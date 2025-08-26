using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Glossaries.Utils.Converters;
using Blackbird.Xliff.Utils;
using Blackbird.Xliff.Utils.Extensions;
using MoreLinq;
using Newtonsoft.Json;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Apps.GoogleVertexAI.Utils;
using Apps.GoogleVertexAI.Models.Entities;
using Blackbird.Xliff.Utils.Models;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.AIPlatform.V1;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json.Linq;
using Apps.GoogleVertexAI.Factories;
using Google.Apis.Storage.v1.Data;
using System.Net;
using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Deprecated XLIFF")]
public class GeminiXliffActions : VertexAiInvocable
{
    private readonly IFileManagementClient _fileManagementClient;

    public GeminiXliffActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Process XLIFF file",
        Description =
            "Processes each translation unit in the XLIFF file according to the provided instructions (by default it just translates the source tags) and updates the target text for each unit. For now it supports only 1.2 version of XLIFF.")]
    public async Task<TranslateXliffResponse> TranslateXliff(
        [ActionParameter] TranslateXliffRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Prompt", Description = "Specify the instruction to be applied to each source tag within a translation unit. For example, 'Translate text'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of source texts to be translated at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize)
    {
        var xliffDocument = await DownloadXliffDocumentAsync(input.File);

        var model = input.AIModel;
        var systemPrompt = GetSystemPrompt(string.IsNullOrEmpty(prompt));
        var list = xliffDocument.TranslationUnits.Select(x => x.Source).ToList();

        var result = await GetTranslations(prompt!, xliffDocument, model, systemPrompt, bucketSize ?? 1500, glossary.Glossary, promptRequest, input);
        result.Translations.ForEach(x =>
        {
            var translationUnit = xliffDocument.TranslationUnits.FirstOrDefault(tu => tu.Id == x.Key);
            if (translationUnit != null)
            {
                translationUnit.Target = x.Value;
            }
        });

        var stream = xliffDocument.ToStream();
        var fileReference = await _fileManagementClient.UploadAsync(stream, input.File.ContentType, input.File.Name);
        return new TranslateXliffResponse { File = fileReference, Usage = result.Usage, Warnings = result.ErrorMessages };
    }

    [Action("Get quality scores for XLIFF file",
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
        var xliffDocument = await DownloadXliffDocumentAsync(input.File);
        var model = input.AIModel;
        var criteriaPrompt = string.IsNullOrEmpty(prompt)
            ? "accuracy, fluency, consistency, style, grammar and spelling"
            : prompt;
        var results = new Dictionary<string, float>();
        var batches = xliffDocument.TranslationUnits.Batch((int)bucketSize);
        var src = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var tgt = input.TargetLanguage ?? xliffDocument.TargetLanguage;

        var usage = new UsageDto();

        foreach (var batch in batches)
        {
            var userPrompt =
                $"Your input is going to be a group of sentences in {src} and their translation into {tgt}. " +
                "Only provide as output the ID of the sentence and the score number as a comma separated array of tuples. " +
                $"Place the tuples in a same line and separate them using semicolons, example for two assessments: 2,7;32,5. The score number is a score from 1 to 10 assessing the quality of the translation, considering the following criteria: {criteriaPrompt}. Sentences: ";
            foreach (var tu in batch)
            {
                userPrompt += $"{tu.Id}: {tu.Source} -> {tu.Target}\n";
            }

            var systemPrompt =
                "You are a linguistic expert that should process the following texts according to the given instructions. Include in your response the ID of the sentence and the score number as a comma separated array of tuples without any additional information (it is crucial because your response will be deserialized programmatically).";
            var (result, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);

            usage += promptUsage;

            try
            {
                foreach (var r in result.Split(";"))
                {
                    var split = r.Split(",");
                    var id = split[0].Trim();
                    var score = float.Parse(split[1].Trim());
                    results.Add(id, score);
                }
            }
            catch (Exception ex)
            {
                throw new PluginApplicationException(
                    $"Failed to parse the LLM response for this batch.\n" +
                    $"Original LLM response:\n{result}\n" +
                    $"Error detail: {ex.Message}", ex);
            }
        }

        results.ForEach(x =>
        {
            var translationUnit = xliffDocument.TranslationUnits.FirstOrDefault(tu => tu.Id == x.Key);
            if (translationUnit != null)
            {
                var attribute = translationUnit.Attributes.FirstOrDefault(x => x.Key == "extradata");
                if (!string.IsNullOrEmpty(attribute.Key))
                {
                    translationUnit.Attributes.Remove(attribute.Key);
                    translationUnit.Attributes.Add("extradata", x.Value.ToString());
                }
                else
                {
                    translationUnit.Attributes.Add("extradata", x.Value.ToString());
                }
            }
        });

        if (input.Threshold != null && input.Condition != null && input.State != null)
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

            filteredTUs.ForEach(x =>
            {
                var translationUnit = xliffDocument.TranslationUnits.FirstOrDefault(tu => tu.Id == x);
                if (translationUnit != null)
                {
                    var stateAttribute = translationUnit.Attributes.FirstOrDefault(x => x.Key == "state");
                    if (!string.IsNullOrEmpty(stateAttribute.Key))
                    {
                        translationUnit.Attributes.Remove(stateAttribute.Key);
                        translationUnit.Attributes.Add("state", input.State);
                    }
                    else
                    {
                        translationUnit.Attributes.Add("state", input.State);
                    }
                }
            });
        }

        var stream = xliffDocument.ToStream();
        return new ScoreXliffResponse
        {
            AverageScore = results.Average(x => x.Value),
            File = await _fileManagementClient.UploadAsync(stream, MediaTypeNames.Text.Xml, input.File.Name),
            Usage = usage,
        };
    }

    [Action("Post-edit XLIFF file",
        Description = "Updates the targets of XLIFF 1.2 files")]
    public async Task<TranslateXliffResponse> PostEditXLIFF(
        [ActionParameter] PostEditXliffRequest input,
        [ActionParameter, Display("Prompt", Description = "Additional instructions")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = 1500)
    {
        if (input.File == null || string.IsNullOrEmpty(input.File.Name))
        {
            throw new PluginMisconfigurationException("The input file is empty. Please check your input and try again");
        }

        var xliffDocument = await DownloadXliffDocumentAsync(input.File);
        var realBucketSize = bucketSize ?? 1500;

        var model = input.AIModel;
        var results = new Dictionary<string, string>();

        var unitsToProcess = FilterTranslationUnits(xliffDocument.TranslationUnits, input.PostEditLockedSegments ?? false, input.ProcessOnlyTargetState);
        var batches = unitsToProcess.Batch(realBucketSize);

        var sourceLanguage = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? xliffDocument.TargetLanguage;
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
                $"Your input consists of sentences in {sourceLanguage} language with their translations into {targetLanguage}. " +
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

        var updatedResults = Blackbird.Xliff.Utils.Utils.XliffExtensions.CheckTagIssues(xliffDocument.TranslationUnits, results);
        updatedResults.ForEach(x =>
        {
            var translationUnit = xliffDocument.TranslationUnits.FirstOrDefault(tu => tu.Id == x.Key);
            if (translationUnit != null)
            {
                translationUnit.Target = x.Value;
            }
        });

        var stream = xliffDocument.ToStream();
        var finalFile = await _fileManagementClient.UploadAsync(stream, input.File.ContentType, input.File.Name);
        return new TranslateXliffResponse { File = finalFile, Usage = usage, };
    }

    [Action("Get translation issues from XLIFF file", Description = "Analyzes an XLIFF file to identify translation issues between source and target texts")]
    public async Task<GetTranslationIssuesResponse> GetTranslationIssues(
        [ActionParameter] GetTranslationIssuesRequest input,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Prompt", Description = "Custom prompt to override the default analysis instructions")] string? prompt = null,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = 1500)
    {
        var xliffDocument = await DownloadXliffDocumentAsync(input.File);
        var model = input.AIModel;
        var unitsToProcess = FilterTranslationUnits(xliffDocument.TranslationUnits, input.PostEditLockedSegments ?? true, input.ProcessOnlyTargetState);

        if (!unitsToProcess.Any())
        {
            throw new PluginMisconfigurationException("No translation units match the specified criteria.");
        }

        var sourceLanguage = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? xliffDocument.TargetLanguage;
        var realBucketSize = bucketSize ?? 1500;
        var batches = unitsToProcess.Batch((int)realBucketSize);
        var usage = new UsageDto();
        var allTranslationIssues = new List<XliffIssueDto>();

        var systemPrompt = prompt ??
            $"You are receiving multiple source texts written in {sourceLanguage} " +
            $"that were translated into target texts written in {targetLanguage}. " +
            "Thoroughly analyze ALL translation units provided in the batch. " +
            "Report translation units that contain errors or issues. " +
            "For each problematic translation unit (identified by its ID), evaluate the target text for grammatical errors, " +
            "language structure issues, and overall linguistic coherence. " +
            $"{(input.TargetAudience != null ? $"Consider that the target audience is {input.TargetAudience}. " : string.Empty)}" +
            "Provide the response as a JSON array of objects with each issue: [{\"id\": \"ID\", \"issues\": \"description of issues\"}]. " +
            "If a translation is correct and has no issues, DO NOT include it in the response. " +
            "This format is critical as your response will be parsed programmatically.";

        if (glossary.Glossary != null)
        {
            systemPrompt +=
                " Ensure that the translation aligns with the glossary entries provided for the respective " +
                "languages, and note any discrepancies, ambiguities, or incorrect usage of terms. Include " +
                "these observations in the issues description.";
        }

        foreach (var batch in batches)
        {
            var userPrompt = new StringBuilder();
            userPrompt.AppendLine($"Please analyze the following translations from {sourceLanguage} to {targetLanguage}:");

            foreach (var unit in batch)
            {
                userPrompt.AppendLine($"ID: {unit.Id}");
                userPrompt.AppendLine($"Source: {unit.Source}");
                userPrompt.AppendLine($"Target: {unit.Target}");
                userPrompt.AppendLine();
            }

            if (glossary.Glossary != null)
            {
                var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, string.Join(';', batch.Select(x => x.Source)));
                if (!string.IsNullOrEmpty(glossaryPromptPart))
                {
                    userPrompt.AppendLine(glossaryPromptPart);
                }
            }

            userPrompt.AppendLine("\nReturn the results as a JSON array strictly following the format specified in the system instructions.");

            var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt.ToString(), systemPrompt);
            usage += promptUsage;

            try
            {
                var batchIssues = GeminiResponseParser.ParseIssuesJson(response, InvocationContext.Logger);
                foreach (var issue in batchIssues)
                {
                    if (!string.IsNullOrEmpty(issue.Id))
                    {
                        var translationUnit = xliffDocument.TranslationUnits.FirstOrDefault(tu => tu.Id == issue.Id);
                        if (translationUnit != null)
                        {
                            issue.Source = translationUnit.Source!;
                            issue.Target = translationUnit.Target!;
                            allTranslationIssues.Add(issue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InvocationContext.Logger?.LogError($"[GoogleGemini] Error processing translation issues: {ex.Message}", []);
                throw new PluginApplicationException(
                    $"Failed to parse the JSON response from Gemini API. Error: {ex.Message}");
            }
        }

        string formattedIssues = XliffIssueFormatter.FormatIssues(allTranslationIssues);
        return new GetTranslationIssuesResponse
        {
            Issues = formattedIssues,
            TranslationIssues = allTranslationIssues,
            Usage = usage
        };
    }

    [Action("Get MQM report from XLIFF file", Description = "Perform an LQA Analysis of the translated XLIFF file. The result will be in the MQM framework form.")]
    public async Task<GetMQMResponse> GetMQMReportFormXliff(
        [ActionParameter] GetTranslationIssuesRequest input,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter] [Display("Additional prompt instructions")]string? AdditionalPrompt,
        [ActionParameter] [Display("System prompt (fully replaces MQM instructions)")] string? customSystemPrompt)
       {
        var xliffDocument = await DownloadXliffDocumentAsync(input.File);
        var model = input.AIModel;
        var unitsToProcess = FilterTranslationUnits(xliffDocument.TranslationUnits, input.PostEditLockedSegments ?? true, input.ProcessOnlyTargetState);

        if (!unitsToProcess.Any())
        {
            throw new PluginMisconfigurationException("No translation units match the specified criteria.");
        }

        var sourceLanguage = input.SourceLanguage ?? xliffDocument.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? xliffDocument.TargetLanguage;
        var allTranslationIssues = new List<XliffIssueDto>();
        var systemPrompt = String.IsNullOrEmpty(customSystemPrompt) ?"Perform an LQA analysis and use the MQM error typology format using all 7 dimensions. " +
                           "Here is a brief description of the seven high-level error type dimensions: " +
                           "1. Terminology – errors arising when a term does not conform to normative domain or organizational terminology standards or when a term in the target text is not the correct, normative equivalent of the corresponding term in the source text. " +
                           "2. Accuracy – errors occurring when the target text does not accurately correspond to the propositional content of the source text, introduced by distorting, omitting, or adding to the message. " +
                           "3. Linguistic conventions  – errors related to the linguistic well-formedness of the text, including problems with grammaticality, spelling, punctuation, and mechanical correctness. " +
                           "4. Style – errors occurring in a text that are grammatically acceptable but are inappropriate because they deviate from organizational style guides or exhibit inappropriate language style. " +
                           "5. Locale conventions – errors occurring when the translation product violates locale-specific content or formatting requirements for data elements. " +
                           "6. Audience appropriateness – errors arising from the use of content in the translation product that is invalid or inappropriate for the target locale or target audience. " +
                           "7. Design and markup – errors related to the physical design or presentation of a translation product, including character, paragraph, and UI element formatting and markup, integration of text with graphical elements, and overall page or window layout. " +
                           "Provide a quality rating for each dimension from 0 (completely bad) to 10 (perfect). You are an expert linguist and your task is to perform a Language Quality Assessment on input sentences. " +
                           "Try to propose a fixed translation that would have no LQA errors. " +
                           "Formatting: use line spacing between each category. The category name should be bold. " : customSystemPrompt;

        if (glossary.Glossary != null)
        {
            systemPrompt +=
                "Use the provided glossary entries for the respective languages. If there are discrepancies " +
                "between the translation and glossary, note them in the 'Terminology' part of the report, " +
                "along with terminology problems not related to the glossary. ";
        }

        if (!String.IsNullOrEmpty(AdditionalPrompt))
        {
            systemPrompt += AdditionalPrompt;
        }

        var tuJson = System.Text.Json.JsonSerializer.Serialize(
          xliffDocument.TranslationUnits.Select(x => new { x.Id, x.Source, x.Target }),
          new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var userPrompt = $"Here are the translation units from {(input.SourceLanguage ?? xliffDocument.SourceLanguage)} into {(input.TargetLanguage ?? xliffDocument.TargetLanguage)}:\n" +
                         tuJson +
                         $"{(input.TargetAudience != null ? $"\nTarget audience: {input.TargetAudience}" : "")}";


        if (glossary.Glossary != null)
            {
                var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, string.Join(';', unitsToProcess.Select(x => x.Source)));
                if (!string.IsNullOrEmpty(glossaryPromptPart))
                {
                    userPrompt = userPrompt + glossaryPromptPart;
                }
            }

        string response = "";
        var promptUsage = new UsageDto();

        try 
        {
            (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt.ToString(), systemPrompt);
        }
        catch (Exception e)
        {
            throw new PluginApplicationException(e.Message);
        }
          return new GetMQMResponse 
        {
            Report = response,
            Usage = promptUsage,
            SystemPrompt = systemPrompt
        }; 
        
    }

    private string BuildPlainTextSummary(IEnumerable<TranslationUnit> units, string issuesText)
    {
        var summary = new StringBuilder();
        summary.AppendLine("Here is an analysis of the provided translations:");
        summary.AppendLine();

        // Try to extract issues by ID from the text
        foreach (var unit in units)
        {
            var idPattern = $@"(?:ID:|id:)[^\d]*{unit.Id}\b";
            var idMatch = Regex.Match(issuesText, idPattern, RegexOptions.IgnoreCase);

            if (idMatch.Success)
            {
                var startIndex = idMatch.Index;
                var nextIdMatch = Regex.Match(issuesText.Substring(startIndex + 1), @"(?:ID:|id:)[^\d]*\d+\b", RegexOptions.IgnoreCase);
                var endIndex = nextIdMatch.Success
                    ? startIndex + 1 + nextIdMatch.Index
                    : issuesText.Length;

                var issueContent = issuesText.Substring(startIndex, endIndex - startIndex).Trim();

                summary.AppendLine($"**ID: {unit.Id}**");
                summary.AppendLine($"*   Source: `{unit.Source}`");
                summary.AppendLine($"*   Target: `{unit.Target}`");
                summary.AppendLine($"*   **Issue(s) identified:**");

                var issueLines = issueContent.Split('\n').Skip(1); // Skip the ID line
                foreach (var line in issueLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        summary.AppendLine($"    *   {line.Trim()}");
                    }
                }

                summary.AppendLine();
            }
        }

        return summary.ToString();
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

    private async Task<GetTranslationsEntity> GetTranslations(string? prompt, XliffDocument xliff, string model,
        string systemPrompt, int bucketSize, FileReference? glossary, PromptRequest promptRequest, TranslateXliffRequest translateXliffRequest)
    {
        var results = new List<string>();
        var errorMessages = new List<string>();
        var unitsToProcess = FilterTranslationUnits(xliff.TranslationUnits, translateXliffRequest.PostEditLockedSegments ?? false, translateXliffRequest.ProcessOnlyTargetState);
        var batches = unitsToProcess.Batch(bucketSize);

        var usageDto = new UsageDto();
        var counter = 1;
        foreach (var batch in batches)
        {
            string json = JsonConvert.SerializeObject(batch.Select(x => "{ID:" + x.Id + "}" + x.Source));
            var userPrompt = GetUserPrompt(prompt, xliff, json);
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

            try
            {
                var result = GeminiResponseParser.ParseStringArray(response, InvocationContext.Logger);
                if (result.IsPartial)
                {
                    errorMessages.Add(
                        $"The response from the Gemini API (batch number: {counter}) was incomplete. " +
                        $"Got {result.Results.Length} results, but expected {batch.Length}. " +
                        "Try to reduce the batch size.");
                }

                results.AddRange(result.Results);
            }
            catch (PluginApplicationException ex)
            {
                InvocationContext.Logger?.LogError($"[GoogleGemini] Failed to parse response: {ex.Message}; Response: {response}", []);
                throw new PluginApplicationException(
                    $"Failed to parse the response from Gemini API. The response format might be invalid or incomplete. Error: {ex.Message}");
            }
            catch (Exception e)
            {
                InvocationContext.Logger?.LogError($"[GoogleGemini] Unexpected error parsing response: {e.Message}; Response: {response}", []);
                throw new PluginApplicationException(
                    $"An unexpected error occurred while parsing the response. Error: {e.Message}");
            }
            finally
            {
                counter++;
            }
        }

        var dict = new Dictionary<string, string>();
        foreach (var item in results)
        {
            var match = Regex.Match(item, "\\{ID:(.*?)\\}(.+)$");
            if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                var id = match.Groups[1].Value;
                var content = match.Groups[2].Value;

                if (!dict.ContainsKey(id))
                {
                    dict[id] = content;
                }
            }
        }

        return new(dict, errorMessages, usageDto);
    }

    private IEnumerable<TranslationUnit> FilterTranslationUnits(IEnumerable<TranslationUnit> units, bool processLocked, string? targetStateToFilter)
    {
        if (!string.IsNullOrEmpty(targetStateToFilter))
        {
            units = units.Where(x => x.TargetAttributes.TryGetValue("state", out string value) && x.TargetAttributes["state"] == targetStateToFilter);
        }

        return processLocked ? units : units.Where(x => !x.IsLocked());
    }

    private string GetUserPrompt(string? prompt, XliffDocument xliffDocument, string json)
    {
        string instruction = string.IsNullOrEmpty(prompt)
            ? $"Translate the following texts from {xliffDocument.SourceLanguage} to {xliffDocument.TargetLanguage}."
            : $"Process the following texts as per the custom instructions: {prompt}. The source language is {xliffDocument.SourceLanguage} and the target language is {xliffDocument.TargetLanguage}. This information might be useful for the custom instructions.";

        return
            $"Please process ALL texts in the provided array. It is critical that you translate EVERY item individually, not just the first one. " +
            $"{instruction} Return the outputs as a serialized JSON array of strings without additional formatting, " +
            $"maintaining the exact same number of elements as the input array. " +
            $"This is crucial because your response will be deserialized programmatically. " +
            $"Do not skip any entries or provide partial results. " +
            $"Original texts (in serialized array format): {json}";
    }    

    protected async Task<XliffDocument> DownloadXliffDocumentAsync(FileReference file)
    {
        var fileStream = await _fileManagementClient.DownloadAsync(file);

        if (fileStream == null)
        {
            throw new PluginMisconfigurationException("Error uploading XLIFF file. Please check you input and try again");
        }

        var xliffMemoryStream = new MemoryStream();
        await fileStream.CopyToAsync(xliffMemoryStream);
        xliffMemoryStream.Position = 0;

        var xliffDocument = xliffMemoryStream.ToXliffDocument();
        if (xliffDocument.TranslationUnits.Count == 0)
        {
            throw new PluginMisconfigurationException("The XLIFF file does not contain any translation units.");
        }

        return xliffDocument;
    }

    [Action("(Batch) Process XLIFF file",
    Description = "Uploads JSONL to GCS and creates a Vertex AI BatchPredictionJob for translating XLIFF.")]
    public async Task<StartBatchXliffResponse> StartXliffBatchTranslation(
    [ActionParameter] TranslateXliffRequest input,
    [ActionParameter] PromptRequest promptRequest,
    [ActionParameter, Display("Prompt")] string? prompt,
    [ActionParameter] GlossaryRequest glossary)
    {
        var xliff = await DownloadXliffDocumentAsync(input.File);
        var units = FilterTranslationUnits(
            xliff.TranslationUnits,
            input.PostEditLockedSegments ?? false,
            input.ProcessOnlyTargetState).ToList();

        if (!units.Any())
            throw new PluginMisconfigurationException("No translation units to process.");

        var systemPrompt = GetSystemPrompt(string.IsNullOrEmpty(prompt));

        string? glossaryText = null;
        if (glossary?.Glossary != null)
        {
            using var glossStream = await _fileManagementClient.DownloadAsync(glossary.Glossary);
            using var sr = new StreamReader(glossStream);
            glossaryText = await sr.ReadToEndAsync();
        }

        var jsonlMs = new MemoryStream();
        using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            foreach (var tu in units)
            {
                var userPrompt = BuildPerUnitPrompt(tu.Id, tu.Source, prompt, glossaryText);
                var req = BuildBatchRequestObject(userPrompt, systemPrompt, promptRequest, input.AIModel);
                var line = JsonConvert.SerializeObject(req, Formatting.None);
                await sw.WriteLineAsync(line);
            }
        }
        jsonlMs.Position = 0;

        var effectiveRegion = ResolveVertexRegion(
            InvocationContext.AuthenticationCredentialsProviders.Get(CredNames.Region).Value);

        var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
        var gcsBucket = await EnsureRegionalBucketAsync(storage, ProjectId, effectiveRegion);

        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var batchIdShort = Guid.NewGuid().ToString("n")[..8];
        var basePrefix = $"xliff/{date}/{batchIdShort}/";

        var inputObject = $"{basePrefix}input.jsonl";
        await storage.UploadObjectAsync(gcsBucket, inputObject, "application/json", jsonlMs);

        var inputUri = $"gs://{gcsBucket}/{inputObject}";
        var outputPrefix = $"gs://{gcsBucket}/{basePrefix}";

        var normalizedModel = NormalizeModelResourceName(input.AIModel);

        var jobClient = ClientFactory.CreateJobService(InvocationContext.AuthenticationCredentialsProviders, effectiveRegion);
        var parent = LocationName.FromProjectLocation(ProjectId, effectiveRegion);

        var job = new BatchPredictionJob
        {
            DisplayName = $"xliff-{effectiveRegion}-{date}-{batchIdShort}",
            Model = normalizedModel,
            InputConfig = new BatchPredictionJob.Types.InputConfig
            {
                InstancesFormat = "jsonl",
                GcsSource = new GcsSource { Uris = { inputUri } }
            },
            OutputConfig = new BatchPredictionJob.Types.OutputConfig
            {
                PredictionsFormat = "jsonl",
                GcsDestination = new GcsDestination { OutputUriPrefix = outputPrefix }
            }
        };

        var created = jobClient.CreateBatchPredictionJob(parent, job);

        return new StartBatchXliffResponse
        {
            JobName = created.Name,
            InputUri = inputUri,
            OutputUriPrefix = outputPrefix,
            Items = units.Count
        };
    }

    [Action("(Batch) Post-edit XLIFF file",
    Description = "Uploads JSONL to GCS and creates a Vertex AI BatchPredictionJob for post-editing targets in XLIFF 1.2.")]
    public async Task<StartBatchXliffResponse> StartXliffPostEditBatch(
    [ActionParameter] PostEditXliffRequest input,
    [ActionParameter, Display("Prompt", Description = "Additional instructions")] string? prompt,
    [ActionParameter] GlossaryRequest glossary,
    [ActionParameter] PromptRequest promptRequest)
    {
        if (input.File == null || string.IsNullOrEmpty(input.File.Name))
            throw new PluginMisconfigurationException("The input file is empty. Please check your input and try again.");

        var xliff = await DownloadXliffDocumentAsync(input.File);
        var units = FilterTranslationUnits(
            xliff.TranslationUnits,
            input.PostEditLockedSegments ?? false,
            input.ProcessOnlyTargetState).ToList();

        if (!units.Any())
            throw new PluginMisconfigurationException("No translation units to process.");

        var sourceLanguage = input.SourceLanguage ?? xliff.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? xliff.TargetLanguage;

        var systemPrompt = "You are a linguistic expert who post-edits translations according to the given instructions.";

        string? glossaryText = null;
        if (glossary?.Glossary != null)
        {
            using var glossStream = await _fileManagementClient.DownloadAsync(glossary.Glossary);
            using var sr = new StreamReader(glossStream);
            glossaryText = await sr.ReadToEndAsync();
        }

        var jsonlMs = new MemoryStream();
        using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            foreach (var tu in units)
            {
                var userPrompt = BuildPerUnitPostEditPrompt(
                    tu.Id, tu.Source, tu.Target, sourceLanguage, targetLanguage, prompt, glossaryText);

                var req = BuildBatchRequestObject(userPrompt, systemPrompt, promptRequest, input.AIModel);
                var line = JsonConvert.SerializeObject(req, Formatting.None);
                await sw.WriteLineAsync(line);
            }
        }
        jsonlMs.Position = 0;

        var effectiveRegion = ResolveVertexRegion(
            InvocationContext.AuthenticationCredentialsProviders.Get(CredNames.Region).Value);

        var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
        var gcsBucket = await EnsureRegionalBucketAsync(storage, ProjectId, effectiveRegion);

        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var batchIdShort = Guid.NewGuid().ToString("n")[..8];
        var basePrefix = $"xliff/{date}/{batchIdShort}/";

        var inputObject = $"{basePrefix}input.jsonl";
        await storage.UploadObjectAsync(gcsBucket, inputObject, "application/json", jsonlMs);

        var inputUri = $"gs://{gcsBucket}/{inputObject}";
        var outputPrefix = $"gs://{gcsBucket}/{basePrefix}";

        var normalizedModel = NormalizeModelResourceName(input.AIModel);

        var jobClient = ClientFactory.CreateJobService(InvocationContext.AuthenticationCredentialsProviders, effectiveRegion);
        var parent = LocationName.FromProjectLocation(ProjectId, effectiveRegion);

        var job = new BatchPredictionJob
        {
            DisplayName = $"xliff-pe-{effectiveRegion}-{date}-{batchIdShort}",
            Model = normalizedModel,
            InputConfig = new BatchPredictionJob.Types.InputConfig
            {
                InstancesFormat = "jsonl",
                GcsSource = new GcsSource { Uris = { inputUri } }
            },
            OutputConfig = new BatchPredictionJob.Types.OutputConfig
            {
                PredictionsFormat = "jsonl",
                GcsDestination = new GcsDestination { OutputUriPrefix = outputPrefix }
            }
        };

        var created = jobClient.CreateBatchPredictionJob(parent, job);

        return new StartBatchXliffResponse
        {
            JobName = created.Name,
            InputUri = inputUri,
            OutputUriPrefix = outputPrefix,
            Items = units.Count
        };
    }


    [Action("(Batch) Download XLIFF from batch",
    Description = "Reads batch output JSONL from GCS, merges into original XLIFF and returns the translated file.")]
    public async Task<TranslateXliffResponse> DownloadXliffFromBatch(
    [ActionParameter, Display("Batch job name")] string jobName,
    [ActionParameter] GetBatchResultRequest originalXliff)
    {
        var region = TryGetLocationFromJobName(jobName, out var loc)
       ? loc
       : ResolveVertexRegion(InvocationContext.AuthenticationCredentialsProviders.Get(CredNames.Region).Value);

        var jobClient = ClientFactory.CreateJobService(
            InvocationContext.AuthenticationCredentialsProviders,
            region);

        var job = jobClient.GetBatchPredictionJob(jobName);

        if (job.State != JobState.Succeeded)
            throw new PluginApplicationException($"Batch job is not ready. Current state: {job.State}");

        var outputPrefix = job.OutputConfig?.GcsDestination?.OutputUriPrefix
            ?? throw new PluginApplicationException("Batch job has no GCS output prefix.");

        if (!outputPrefix.StartsWith("gs://"))
            throw new PluginApplicationException("Unexpected output URI prefix.");
        var noScheme = outputPrefix.Substring("gs://".Length);
        var slash = noScheme.IndexOf('/');
        var bucket = slash >= 0 ? noScheme.Substring(0, slash) : noScheme;
        var prefix = slash >= 0 ? noScheme.Substring(slash + 1) : "";

        var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
        var objects = storage.ListObjects(bucket, prefix)
            .Where(o => o.Name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.Name)
            .ToList();

        if (!objects.Any())
            throw new PluginApplicationException("No JSONL outputs found in batch destination.");

        var translations = new Dictionary<string, string>();
        var usage = new UsageDto();
        var warnings = new List<string>();

        foreach (var obj in objects)
        {
            using var ms = new MemoryStream();
            await storage.DownloadObjectAsync(obj, ms);
            ms.Position = 0;

            using var sr = new StreamReader(ms);
            string? line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var jo = JObject.Parse(line);

                var resp = jo["response"];
                if (resp == null)
                {
                    var status = jo["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status)) warnings.Add(status);
                    continue;
                }

                var usageNode = resp["usageMetadata"];
                if (usageNode != null)
                {
                    usage += new UsageDto(new GenerateContentResponse.Types.UsageMetadata
                    {
                        PromptTokenCount = (int?)usageNode["promptTokenCount"] ?? 0,
                        CandidatesTokenCount = (int?)usageNode["candidatesTokenCount"] ?? 0,
                        TotalTokenCount = (int?)usageNode["totalTokenCount"] ?? 0
                    });
                }

                var text = ExtractTextFromResponse(resp);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var m = Regex.Match(text, "\\{ID:(.*?)\\}(.+)$", RegexOptions.Singleline);
                if (!m.Success) { warnings.Add("Cannot find ID prefix in: " + Truncate(text)); continue; }

                var id = m.Groups[1].Value.Trim();
                var content = m.Groups[2].Value.Trim();
                if (!translations.ContainsKey(id))
                    translations[id] = content;
            }
        }

        var xliff = await DownloadXliffDocumentAsync(originalXliff.OriginalXliff);
        foreach (var tu in xliff.TranslationUnits)
        {
            if (translations.TryGetValue(tu.Id, out var tgt))
                tu.Target = tgt;
        }

        var outStream = xliff.ToStream();
        var outFile = await _fileManagementClient.UploadAsync(outStream, originalXliff.OriginalXliff.ContentType ?? "application/xml",
            Path.GetFileNameWithoutExtension(originalXliff.OriginalXliff.Name) + ".translated.xliff");

        return new TranslateXliffResponse { File = outFile, Usage = usage, Warnings = warnings };
    }

    private static string BuildPerUnitPostEditPrompt(string id,string source,string target,string sourceLang, string targetLang,string? userInstruction,string? glossaryText)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Your input contains a source sentence in {sourceLang} and its translation into {targetLang}.");
        sb.AppendLine("Review and edit the translated target text so that it is a correct and accurate translation of the source text.");
        sb.AppendLine("If you see XML tags in the source, include them in the target text and do not delete or modify them.");
        if (!string.IsNullOrWhiteSpace(userInstruction))
        {
            sb.AppendLine();
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(userInstruction.Trim());
        }
        if (!string.IsNullOrWhiteSpace(glossaryText))
        {
            sb.AppendLine();
            sb.AppendLine("Use the following glossary where applicable to ensure terminology consistency:");
            sb.AppendLine(glossaryText);
        }

        sb.AppendLine();
        sb.AppendLine($"Text ID: {id}");
        sb.AppendLine("Source: " + source);
        sb.AppendLine("Current target: " + target);

        sb.AppendLine($"Return ONLY the final target text in the format [ID:{id}]{{target}} and nothing else.");

        return sb.ToString();
    }


    private static string ExtractTextFromResponse(JToken response)
    {
        var parts = response["candidates"]?.First?["content"]?["parts"] as JArray;
        if (parts == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            var t = (string?)p["text"];
            if (!string.IsNullOrEmpty(t)) sb.Append(t);
        }
        return sb.ToString();
    }

    private static bool TryGetLocationFromJobName(string jobName, out string location)
    {
        location = string.Empty;

        if (BatchPredictionJobName.TryParse(jobName, out var rn))
        {
            location = rn.LocationId;
            return !string.IsNullOrWhiteSpace(location);
        }
        var m = Regex.Match(jobName,
            @"^projects/[^/]+/locations/(?<loc>[^/]+)/batchPredictionJobs/[^/]+$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            location = m.Groups["loc"].Value;
            return true;
        }

        return false;
    }
    private static string Truncate(string s, int max = 120)
        => s.Length <= max ? s : s.Substring(0, max) + "…";

    private string ResolveVertexRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region)) return "us-central1";
        var r = region.Trim().ToLowerInvariant();
        return r == "global" ? "us-central1" : r;
    }

    private string ResolveGcsLocation(string region)
    {
        var r = ResolveVertexRegion(region);
        if (r is "us" or "eu" or "asia") return r.ToUpperInvariant();
        return r;
    }
    private static string NormalizeModelResourceName(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new PluginMisconfigurationException("Model is required.");

        var m = model.Trim();

        if (m.StartsWith("projects/", StringComparison.OrdinalIgnoreCase)) return m;
        if (m.StartsWith("publishers/", StringComparison.OrdinalIgnoreCase)) return m;

        if (m.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            return $"publishers/google/{m}";

        return $"publishers/google/models/{m}";
    }

    private async Task<string> EnsureRegionalBucketAsync(StorageClient storage, string projectId, string region)
    {
        var bucketName = $"blackbird-batch-{projectId}-{region}"
         .ToLowerInvariant()
         .Replace("_", "-");

        try
        {
            var existing = await storage.GetBucketAsync(bucketName);
            if (existing == null)
                throw new PluginApplicationException($"Bucket '{bucketName}' could not be retrieved.");

            if (!IsCompatibleLocation(existing.Location, region))
            {
                throw new PluginApplicationException(
                    $"Bucket '{bucketName}' is in '{existing.Location}', but job region is '{region}'. " +
                    $"Use a compatible location (same region or a multi-region that includes it), " +
                    $"or let the action create one automatically.");
            }

            return bucketName;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            var bucket = new Bucket
            {
                Name = bucketName,
                Location = region,
                StorageClass = "STANDARD",
                IamConfiguration = new Bucket.IamConfigurationData
                {
                    UniformBucketLevelAccess = new Bucket.IamConfigurationData.UniformBucketLevelAccessData { Enabled = true }
                }
            };

            try
            {
                await storage.CreateBucketAsync(projectId, bucket);
                return bucketName;
            }
            catch (Google.GoogleApiException createEx) when (createEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                var alt = $"{bucketName}-{Guid.NewGuid().ToString("n")[..6]}";
                var altBucket = new Bucket
                {
                    Name = alt,
                    Location = region,
                    StorageClass = "STANDARD",
                    IamConfiguration = bucket.IamConfiguration
                };
                await storage.CreateBucketAsync(projectId, altBucket);
                return alt;
            }
        }
    }

    private static bool IsCompatibleLocation(string? bucketLocation, string region)
    {
        if (string.IsNullOrWhiteSpace(bucketLocation)) return false;

        var b = bucketLocation.Trim().ToLowerInvariant();
        var r = region.Trim().ToLowerInvariant();

        if (b == r) return true;

        if (b == "us" && r.StartsWith("us-")) return true;
        if (b == "eu" && (r.StartsWith("europe-") || r.StartsWith("eu-"))) return true;
        if (b == "asia" && r.StartsWith("asia-")) return true;

        return false;
    }

    private static string NormalizePrefix(string? prefix)
    {
        var p = string.IsNullOrWhiteSpace(prefix) ? "blackbird-batch/" : prefix.Trim();
        return p.EndsWith("/") ? p : p + "/";
    }

    private static bool SameLocation(string? bucketLocation, string region) =>
        !string.IsNullOrWhiteSpace(bucketLocation) &&
        string.Equals(bucketLocation, region, StringComparison.OrdinalIgnoreCase);

    private static object BuildBatchRequestObject(string userPrompt, string systemPrompt, PromptRequest pr, string modelPath)
    {
        var genCfg = new Dictionary<string, object>();
        if (pr.Temperature.HasValue) genCfg["temperature"] = pr.Temperature.Value;
        if (pr.TopP.HasValue) genCfg["topP"] = pr.TopP.Value;
        if (pr.TopK.HasValue) genCfg["topK"] = pr.TopK.Value;
        if (pr.MaxOutputTokens.HasValue) genCfg["maxOutputTokens"] = pr.MaxOutputTokens.Value;

        var system = new { parts = new[] { new { text = systemPrompt } } };
        var contents = new[]
        {
        new
        {
            role = "user",
            parts = new object[] { new { text = userPrompt } }
        }
            };

        if (genCfg.Count == 0)
        {
            return new
            {
                request = new
                {
                    systemInstruction = system,
                    contents = contents
                }
            };
        }
        else
        {
            return new
            {
                request = new
                {
                    systemInstruction = system,
                    contents = contents,
                    generationConfig = genCfg
                }
            };
        }
    }

    private static string BuildPerUnitPrompt(string id, string source, string? userInstruction, string? glossaryText)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(userInstruction))
            sb.AppendLine(userInstruction.Trim());
        else
            sb.AppendLine("Translate the text.");

        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(glossaryText))
        {
            sb.AppendLine("Use the following glossary where applicable to ensure terminology consistency:");
            sb.AppendLine(glossaryText);
            sb.AppendLine();
        }

        sb.AppendLine($"Text ID: {id}");
        sb.Append("Source: ").AppendLine(source);
        sb.AppendLine("Return ONLY the processed target text, prefixed with {ID:" + id + "} and nothing else.");

        return sb.ToString();
    }
}