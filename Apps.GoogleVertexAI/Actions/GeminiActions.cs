using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Extensions;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Requests.Gemini;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Glossaries.Utils.Converters;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Xliff.Utils;
using Blackbird.Xliff.Utils.Extensions;
using Blackbird.Xliff.Utils.Models;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using MoreLinq;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Actions;

[ActionList]
public class GeminiActions : VertexAiInvocable
{
    private readonly IFileManagementClient _fileManagementClient;

    public GeminiActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Generate text with Gemini", Description = "Generate text using Gemini. Text generation based on a " +
                                                       "single prompt is executed with the gemini-1.0-pro model. " +
                                                       "Optionally, you can specify an image or video, and the " +
                                                       "generation will be performed with the gemini-1.0-pro-vision " +
                                                       "model.")]
    public async Task<GeneratedTextResponse> GenerateText([ActionParameter] GenerateTextRequest input)
    {
        if (input.Image != null && input.Video != null)
            throw new Exception("Please include either an image or a video, but not both.");

        var prompt = input.Prompt;
        var modelId = ModelIds.GeminiPro;
        Part? inlineDataPart = null;

        if (input.IsBlackbirdPrompt is true)
            prompt = input.Prompt.FromBlackbirdPrompt();

        if (input.Image != null)
        {
            await using var imageStream = await _fileManagementClient.DownloadAsync(input.Image);
            var imageBytes = await imageStream.GetByteData();
            modelId = ModelIds.GeminiProVision;
            inlineDataPart = new Part
            {
                InlineData = new Blob { Data = ByteString.CopyFrom(imageBytes), MimeType = input.Image.ContentType }
            };
        }

        if (input.Video != null)
        {
            await using var videoStream = await _fileManagementClient.DownloadAsync(input.Video);
            var videoBytes = await videoStream.GetByteData();
            modelId = ModelIds.GeminiProVision;
            inlineDataPart = new Part
            {
                InlineData = new Blob { Data = ByteString.CopyFrom(videoBytes), MimeType = input.Video.ContentType }
            };
        }

        var safetySettings = input is { SafetyCategories: not null, SafetyCategoryThresholds: not null }
            ? input.SafetyCategories
                .Take(Math.Min(input.SafetyCategories.Count(), input.SafetyCategoryThresholds.Count()))
                .Zip(input.SafetyCategoryThresholds,
                    (category, threshold) => new SafetySetting
                    {
                        Category = Enum.Parse<HarmCategory>(category),
                        Threshold = Enum.Parse<SafetySetting.Types.HarmBlockThreshold>(threshold)
                    })
            : Enumerable.Empty<SafetySetting>();

        var endpoint = input.ModelEndpoint ?? EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google, modelId)
            .ToString();

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        if (inlineDataPart != null)
            content.Parts.Add(inlineDataPart);

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? (modelId == ModelIds.GeminiPro ? 0.9f : 0.4f),
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? (modelId == ModelIds.GeminiPro ? 3 : 32),
                MaxOutputTokens = input.MaxOutputTokens ?? (modelId == ModelIds.GeminiPro ? 8192 : 2048)
            },
            SafetySettings = { safetySettings }
        };
        generateContentRequest.Contents.Add(content);

        try
        {
            using var response = Client.StreamGenerateContent(generateContentRequest);
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();

            await foreach (var responseItem in responseStream)
            {
                generatedText.Append(responseItem.Candidates[0].Content?.Parts[0].Text ?? string.Empty);
            }

            return new() { GeneratedText = generatedText.ToString() };
        }
        catch (Exception exception)
        {
            throw new Exception(exception.Message);
        }
    }

    [Action("Process XLIFF file",
        Description =
            "Processes each translation unit in the XLIFF file according to the provided instructions (by default it just translates the source tags) and updates the target text for each unit. For now it supports only 1.2 version of XLIFF.")]
    public async Task<TranslateXliffResponse> TranslateXliff(
        [ActionParameter] TranslateXliffRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter,
         Display("Prompt",
             Description =
                 "Specify the instruction to be applied to each source tag within a translation unit. For example, 'Translate text'")]
        string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter,
         Display("Bucket size",
             Description =
                 "Specify the number of source texts to be translated at once. Default value: 1500. (See our documentation for an explanation)")]
        int? bucketSize = 1500)
    {
        var xliffDocument = await LoadAndParseXliffDocument(input.File);
        if (xliffDocument.TranslationUnits.Count == 0)
        {
            return new TranslateXliffResponse { File = input.File, Usage = new UsageDto() };
        }

        var model = promptRequest.ModelEndpoint ?? ModelIds.GeminiPro;
        string systemPrompt = GetSystemPrompt(string.IsNullOrEmpty(prompt));
        var list = xliffDocument.TranslationUnits.Select(x => x.Source).ToList();

        var (translatedTexts, usage) = await GetTranslations(prompt, xliffDocument, model, systemPrompt, list,
            bucketSize ?? 15,
            glossary.Glossary, promptRequest);

        var updatedDocument =
            UpdateXliffDocumentWithTranslations(xliffDocument, translatedTexts, input.UpdateLockedSegments ?? false);
        var fileReference = await UploadUpdatedDocument(updatedDocument, input.File);
        return new TranslateXliffResponse { File = fileReference, Usage = usage };
    }

    [Action("Get Quality Scores for XLIFF file",
        Description = "Gets segment and file level quality scores for XLIFF files")]
    public async Task<ScoreXliffResponse> ScoreXLIFF(
        [ActionParameter] ScoreXliffRequest input, [ActionParameter,
                                                    Display("Prompt",
                                                        Description =
                                                            "Add any linguistic criteria for quality evaluation")]
        string? prompt,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter,
         Display("Bucket size",
             Description =
                 "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")]
        int? bucketSize = 1500)
    {
        var xliffDocument = await LoadAndParseXliffDocument(input.File);
        var model = promptRequest.ModelEndpoint ?? ModelIds.GeminiPro;
        string criteriaPrompt = string.IsNullOrEmpty(prompt)
            ? "accuracy, fluency, consistency, style, grammar and spelling"
            : prompt;
        var results = new Dictionary<string, float>();
        var batches = xliffDocument.TranslationUnits.Batch((int)bucketSize);
        var src = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var tgt = input.TargetLanguage ?? xliffDocument.TargetLanguage;

        var usage = new UsageDto();

        foreach (var batch in batches)
        {
            string userPrompt =
                $"Your input is going to be a group of sentences in {src} and their translation into {tgt}. " +
                "Only provide as output the ID of the sentence and the score number as a comma separated array of tuples. " +
                $"Place the tuples in a same line and separate them using semicolons, example for two assessments: 2,7;32,5. The score number is a score from 1 to 10 assessing the quality of the translation, considering the following criteria: {criteriaPrompt}. Sentences: ";
            foreach (var tu in batch)
            {
                userPrompt += $" {tu.Id} {tu.Source} {tu.Target}";
            }

            var systemPrompt =
                "You are a linguistic expert that should process the following texts accoring to the given instructions";
            var (result, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);
            usage += promptUsage;

            foreach (var r in result.Split(";"))
            {
                var split = r.Split(",");
                results.Add(split[0], float.Parse(split[1]));
            }
        }

        var file = await _fileManagementClient.DownloadAsync(input.File);
        string fileContent;
        Encoding encoding;
        using (var inFileStream = new StreamReader(file, true))
        {
            encoding = inFileStream.CurrentEncoding;
            fileContent = inFileStream.ReadToEnd();
        }

        foreach (var r in results)
        {
            fileContent = Regex.Replace(fileContent, @"(<trans-unit id=""" + r.Key + @""")",
                @"${1} extradata=""" + r.Value + @"""");
        }

        if (input is { Threshold: not null, Condition: not null, State: not null })
        {
            var filteredTUs = new List<string>();
            switch (input.Condition)
            {
                case ">":
                    filteredTUs = results.Where(x => x.Value > input.Threshold).Select(x => x.Key).ToList();
                    break;
                case ">=":
                    filteredTUs = results.Where(x => x.Value >= input.Threshold).Select(x => x.Key).ToList();
                    break;
                case "=":
                    filteredTUs = results.Where(x => x.Value == input.Threshold).Select(x => x.Key).ToList();
                    break;
                case "<":
                    filteredTUs = results.Where(x => x.Value < input.Threshold).Select(x => x.Key).ToList();
                    break;
                case "<=":
                    filteredTUs = results.Where(x => x.Value <= input.Threshold).Select(x => x.Key).ToList();
                    break;
            }

            fileContent = UpdateTargetState(fileContent, input.State, filteredTUs);
        }

        return new ScoreXliffResponse
        {
            AverageScore = results.Average(x => x.Value),
            File = await _fileManagementClient.UploadAsync(new MemoryStream(encoding.GetBytes(fileContent)),
                MediaTypeNames.Text.Xml, input.File.Name),
            Usage = usage,
        };
    }

    [Action("Post-edit XLIFF file",
        Description = "Updates the targets of XLIFF 1.2 files")]
    public async Task<TranslateXliffResponse> PostEditXLIFF(
        [ActionParameter] PostEditXliffRequest input, [ActionParameter,
                                                       Display("Prompt",
                                                           Description =
                                                               "Additional instructions")]
        string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter,
         Display("Bucket size",
             Description =
                 "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")]
        int? bucketSize = 1500)
    {
        var fileStream = await _fileManagementClient.DownloadAsync(input.File);
        var xliffDocument = Utils.Xliff.Extensions.ParseXLIFF(fileStream);

        var model = promptRequest.ModelEndpoint ?? ModelIds.GeminiPro;
        var results = new Dictionary<string, string>();
        var batches = xliffDocument.TranslationUnits.Batch((int)bucketSize);
        var src = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var tgt = input.TargetLanguage ?? xliffDocument.TargetLanguage;
        var usage = new UsageDto();

        foreach (var batch in batches)
        {
            string? glossaryPrompt = null;
            if (glossary?.Glossary != null)
            {
                var glossaryPromptPart =
                    await GetGlossaryPromptPart(glossary.Glossary,
                        string.Join(';', batch.Select(x => x.Source)) + ";" +
                        string.Join(';', batch.Select(x => x.Target)));
                if (glossaryPromptPart != null)
                {
                    glossaryPrompt +=
                        "Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                        "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                        "If a term has variations or synonyms, consider them and choose the most appropriate " +
                        "translation to maintain consistency and precision. ";
                    glossaryPrompt += glossaryPromptPart;
                }
            }

            var userPrompt =
                $"Your input consists of sentences in {src} language with their translations into {tgt}. " +
                "Review and edit the translated target text as necessary to ensure it is a correct and accurate translation of the source text. " +
                "If you see XML tags in the source also include them in the target text, don't delete or modify them. " +
                "Include only the target texts (updated or not) in the format [ID:X]{target}. " +
                $"Example: [ID:1]{{target1}},[ID:2]{{target2}}. " +
                $"{prompt ?? ""} {glossaryPrompt ?? ""} Sentences: \n" +
                string.Join("\n", batch.Select(tu => $"ID: {tu.Id}; Source: {tu.Source}; Target: {tu.Target}"));

            var systemPrompt =
                "You are a linguistic expert that should process the following texts according to the given instructions";
            var (result, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);
            usage += promptUsage;

            var matches = Regex.Matches(result, @"\[ID:(.+?)\]\{([\s\S]+?)\}(?=,\[|$|,?\n)").Cast<Match>().ToList();
            foreach (var match in matches)
            {
                if (match.Groups[2].Value.Contains("[ID:"))
                    continue;
                results.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
        }

        var updatedResults = Utils.Xliff.Extensions.CheckTagIssues(xliffDocument.TranslationUnits, results);
        var originalFile = await _fileManagementClient.DownloadAsync(input.File);
        var updatedFile = Utils.Xliff.Extensions.UpdateOriginalFile(originalFile, updatedResults);

        var finalFile = await _fileManagementClient.UploadAsync(updatedFile, input.File.ContentType, input.File.Name);
        return new TranslateXliffResponse { File = finalFile, Usage = usage, };
    }

    private async Task<XliffDocument> LoadAndParseXliffDocument(FileReference inputFile)
    {
        var stream = await _fileManagementClient.DownloadAsync(inputFile);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return memoryStream.ToXliffDocument();
    }

    private async Task<string?> GetGlossaryPromptPart(FileReference glossary, string sourceContent)
    {
        var glossaryStream = await _fileManagementClient.DownloadAsync(glossary);
        var blackbirdGlossary = await glossaryStream.ConvertFromTbx();

        var glossaryPromptPart = new StringBuilder();
        glossaryPromptPart.AppendLine();
        glossaryPromptPart.AppendLine();
        glossaryPromptPart.AppendLine("Glossary entries (each entry includes terms in different language. Each " +
                                      "language may have a few synonymous variations which are separated by ;;):");

        var entriesIncluded = false;
        foreach (var entry in blackbirdGlossary.ConceptEntries)
        {
            var allTerms = entry.LanguageSections.SelectMany(x => x.Terms.Select(y => y.Term));
            if (!allTerms.Any(x => Regex.IsMatch(sourceContent, $@"\b{x}\b", RegexOptions.IgnoreCase))) continue;
            entriesIncluded = true;

            glossaryPromptPart.AppendLine();
            glossaryPromptPart.AppendLine("\tEntry:");

            foreach (var section in entry.LanguageSections)
            {
                glossaryPromptPart.AppendLine(
                    $"\t\t{section.LanguageCode}: {string.Join(";; ", section.Terms.Select(term => term.Term))}");
            }
        }

        return entriesIncluded ? glossaryPromptPart.ToString() : null;
    }

    private string UpdateTargetState(string fileContent, string state, List<string> filteredTUs)
    {
        var tus = Regex.Matches(fileContent, @"<trans-unit[\s\S]+?</trans-unit>").Select(x => x.Value);
        foreach (var tu in tus.Where(x =>
                     filteredTUs.Any(y => y == Regex.Match(x, @"<trans-unit id=""(\d+)""").Groups[1].Value)))
        {
            string transformedTU = Regex.IsMatch(tu, @"<target(.*?)state=""(.*?)""(.*?)>")
                ? Regex.Replace(tu, @"<target(.*?state="")(.*?)("".*?)>", @"<target${1}" + state + "${3}>")
                : Regex.Replace(tu, "<target", @"<target state=""" + state + @"""");
            fileContent = Regex.Replace(fileContent, Regex.Escape(tu), transformedTU);
        }

        return fileContent;
    }

    private string GetSystemPrompt(bool translator)
    {
        string prompt;
        if (translator)
        {
            prompt =
                "You are tasked with localizing the provided text. Consider cultural nuances, idiomatic expressions, " +
                "and locale-specific references to make the text feel natural in the target language. " +
                "Ensure the structure of the original text is preserved. Respond with the localized text.";
        }
        else
        {
            prompt =
                "You will be given a list of texts. Each text needs to be processed according to specific instructions " +
                "that will follow. " +
                "The goal is to adapt, modify, or translate these texts as required by the provided instructions. " +
                "Prepare to process each text accordingly and provide the output as instructed.";
        }

        prompt +=
            "Please note that each text is considered as an individual item for translation. Even if there are entries " +
            "that are identical or similar, each one should be processed separately. This is crucial because the output " +
            "should be an array with the same number of elements as the input. This array will be used programmatically, " +
            "so maintaining the same element count is essential.";

        return prompt;
    }

    private async Task<(string[], UsageDto)> GetTranslations(string prompt, XliffDocument xliffDocument, string model,
        string systemPrompt, List<string> sourceTexts, int bucketSize, FileReference? glossary,
        PromptRequest promptRequest)
    {
        List<string> allTranslatedTexts = new List<string>();

        int numberOfBuckets = (int)Math.Ceiling(sourceTexts.Count / (double)bucketSize);

        var usageDto = new UsageDto();
        for (int i = 0; i < numberOfBuckets; i++)
        {
            var bucketIndexOffset = i * bucketSize;
            var bucketSourceTexts = sourceTexts
                .Skip(bucketIndexOffset)
                .Take(bucketSize)
                .Select((text, index) => "{ID:" + $"{bucketIndexOffset + index}" + "}" + $"{text}")
                .ToList();

            string json = JsonConvert.SerializeObject(bucketSourceTexts);

            var userPrompt = GetUserPrompt(prompt, xliffDocument, json);

            if (glossary != null)
            {
                var glossaryPromptPart = await GetGlossaryPromptPart(glossary, json);
                if (glossaryPromptPart != null)
                {
                    var glossaryPrompt =
                        "Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                        "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                        "If a term has variations or synonyms, consider them and choose the most appropriate " +
                        "translation to maintain consistency and precision. ";
                    glossaryPrompt += glossaryPromptPart;
                    userPrompt += glossaryPrompt;
                }
            }

            var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);

            usageDto += promptUsage;
            var translatedText = response.Trim()
                .Replace("```", string.Empty).Replace("json", string.Empty);

            try
            {
                var result = JsonConvert.DeserializeObject<string[]>(translatedText)
                    .Select(t =>
                    {
                        int idEndIndex = t.IndexOf('}') + 1;
                        return idEndIndex < t.Length ? t.Substring(idEndIndex) : string.Empty;
                    })
                    .ToArray();

                if (result.Length != bucketSourceTexts.Count)
                {
                    throw new InvalidOperationException(
                        "OpenAI returned inappropriate response. " +
                        "The number of translated texts does not match the number of source texts. " +
                        "Probably there is a duplication or a missing text in translation unit. " +
                        "Try change model or bucket size (to lower values) or add retries to this action.");
                }

                allTranslatedTexts.AddRange(result);
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Failed to parse the translated text in bucket {i + 1}. Exception message: {e.Message}; Exception type: {e.GetType()}");
            }
        }

        return (allTranslatedTexts.ToArray(), usageDto);
    }

    string GetUserPrompt(string prompt, XliffDocument xliffDocument, string json)
    {
        string instruction = string.IsNullOrEmpty(prompt)
            ? $"Translate the following texts from {xliffDocument.SourceLanguage} to {xliffDocument.TargetLanguage}."
            : $"Process the following texts as per the custom instructions: {prompt}. The source language is {xliffDocument.SourceLanguage} and the target language is {xliffDocument.TargetLanguage}. This information might be useful for the custom instructions.";

        return
            $"Please provide a translation for each individual text, even if similar texts have been provided more than once. " +
            $"{instruction} Return the outputs as a serialized JSON array of strings without additional formatting " +
            $"(it is crucial because your response will be deserialized programmatically. Please ensure that your response is formatted correctly to avoid any deserialization issues). " +
            $"Original texts (in serialized array format): {json}";
    }

    private XliffDocument UpdateXliffDocumentWithTranslations(XliffDocument xliffDocument, string[] translatedTexts,
        bool updateLockedSegments)
    {
        var updatedUnits = xliffDocument.TranslationUnits.Zip(translatedTexts, (unit, translation) =>
        {
            if (updateLockedSegments == false && unit.Attributes is not null &&
                unit.Attributes.Any(x => x.Key == "locked" && x.Value == "locked"))
            {
                unit.Target = unit.Target;
            }
            else
            {
                unit.Target = translation;
            }

            return unit;
        }).ToList();

        var xDoc = xliffDocument.UpdateTranslationUnits(updatedUnits);
        var stream = new MemoryStream();
        xDoc.Save(stream);
        stream.Position = 0;

        return stream.ToXliffDocument(new XliffConfig
            { RemoveWhitespaces = true, CopyAttributes = true, IncludeInlineTags = true });
    }

    private async Task<FileReference> UploadUpdatedDocument(XliffDocument xliffDocument, FileReference originalFile)
    {
        var outputMemoryStream = xliffDocument.ToStream(null, false, keepSingleAmpersands: true);

        string contentType = originalFile.ContentType ?? "application/xml";
        return await _fileManagementClient.UploadAsync(outputMemoryStream, contentType, originalFile.Name);
    }

    private async Task<(string result, UsageDto usage)> ExecuteGeminiPrompt(PromptRequest input, string modelId,
        string prompt,
        string? systemPrompt = null)
    {
        var endpoint = EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google, modelId)
            .ToString();

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        var safetySettings = input is { SafetyCategories: not null, SafetyCategoryThresholds: not null }
            ? input.SafetyCategories
                .Take(Math.Min(input.SafetyCategories.Count(), input.SafetyCategoryThresholds.Count()))
                .Zip(input.SafetyCategoryThresholds,
                    (category, threshold) => new SafetySetting
                    {
                        Category = Enum.Parse<HarmCategory>(category),
                        Threshold = Enum.Parse<SafetySetting.Types.HarmBlockThreshold>(threshold)
                    })
            : Enumerable.Empty<SafetySetting>();

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? (modelId == ModelIds.GeminiPro ? 0.9f : 0.4f),
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? (modelId == ModelIds.GeminiPro ? 3 : 32),
                MaxOutputTokens = input.MaxOutputTokens ?? (modelId == ModelIds.GeminiPro ? 8192 : 2048)
            },
            SafetySettings = { safetySettings },
            SystemInstruction = systemPrompt is null
                ? null
                : new()
                {
                    Parts =
                    {
                        new Part { Text = systemPrompt },
                    }
                }
        };
        generateContentRequest.Contents.Add(content);

        try
        {
            using var response = Client.StreamGenerateContent(generateContentRequest);
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();

            var usage = new UsageDto();
            await foreach (var responseItem in responseStream)
            {
                if (responseItem.UsageMetadata is not null)
                    usage += new UsageDto(responseItem.UsageMetadata);

                generatedText.Append(responseItem.Candidates[0].Content.Parts[0].Text);
            }

            return (generatedText.ToString(), usage);
        }
        catch (Exception exception)
        {
            throw new Exception(exception.Message);
        }
    }
}