using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json;
using System.Text;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Filters.Extensions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Translation")]
public class TranslationActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
    protected const string SystemPrompt = 
        "You are tasked with localizing the provided text. Consider cultural nuances, idiomatic expressions, " +
       "and locale-specific references to make the text feel natural in the target language. " +
       "Ensure the structure of the original text is preserved. Respond with the localized text." +
       "Please note that each text is considered as an individual item for translation. Even if there are entries " +
       "that are identical or similar, each one should be processed separately. This is crucial because the output " +
       "should be an array with the same number of elements as the input. This array will be used programmatically, " +
       "so maintaining the same element count is essential.";

    [BlueprintActionDefinition(BlueprintAction.TranslateFile)]
    [Action("Translate", Description = "Translate file content retrieved from a CMS or file storage. The output can be used in compatible actions.")]
    public async Task<FileTranslationResponse> TranslateContent(
        [ActionParameter] TranslateFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of source texts to be translated at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        var batchSize = bucketSize ?? 1500;
        var model = input.AIModel;
        var result = new FileTranslationResponse();
        var stream = await fileManagementClient.DownloadAsync(input.File);
        
        var loadResult = Transformation.Load(stream, input.File.Name);
        if (!loadResult.Success)
            throw new PluginMisconfigurationException(loadResult.Error);

        var content = loadResult.Value;
        content.SourceLanguage ??= input.SourceLanguage;
        content.TargetLanguage ??= input.TargetLanguage;
        if (content.TargetLanguage == null) throw new PluginMisconfigurationException("The target language is not defined yet. Please assign the target language in this action.");

        if (content.SourceLanguage == null)
        {
            var sourceLoadResult = content.Source();
            if (!sourceLoadResult.Success)
            {
                throw new PluginMisconfigurationException(
                    loadResult.Error ?? "An unknown error occured while parsing the content");
            }

            var source = sourceLoadResult.Value;
            content.SourceLanguage = await IdentifySourceLanguage(promptRequest, model, source.GetPlaintext());
        }

        var counter = 1;        
        var errorMessages = new List<string>();

        async Task<IEnumerable<string?>> BatchTranslate(IEnumerable<(Unit Unit, Segment Segment)> batch)
        {
            var inputObjects = batch.Select((x, index) => new 
            { 
                id = index, 
                text = x.Segment.GetSource() 
            });
            var json = JsonConvert.SerializeObject(inputObjects);
            
            var userPrompt = 
                $"Translate the following texts from {content.SourceLanguage} to {content.TargetLanguage}. " +
                $"Return ONLY a JSON array of strings containing the translations in the exact same order. " +
                $"Do not skip any entries. {prompt}\n" +
                $"Original texts: {json}";

            if (glossary.Glossary != null)
            {
                var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary, json);
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

            var (response, promptUsage) = await ExecuteGeminiPrompt(
                promptRequest, 
                model, 
                userPrompt, 
                SystemPrompt, 
                ResponseSchemas.IdTranslationArray);

            try
            {
                var translations = JsonConvert.DeserializeObject<TranslationResultDto[]>(response) ?? 
                    throw new PluginApplicationException("The Gemini API returned an empty or null JSON array.");

                if (translations.Length != batch.Count())
                {
                    errorMessages.Add(
                        $"The response from the Gemini API (batch number: {counter}) was incomplete. " +
                        $"Got {translations.Length} results, but expected {batch.Count()}. " +
                        "Try to reduce the batch size.");
                }

                return translations.OrderBy(x => x.Id).Select(x => x.Translation);
            }
            catch (Exception ex)
            {
                InvocationContext.Logger?.LogError(
                    $"[GoogleGemini] Failed to parse response: {ex.Message}; Response: {response}", []);
                throw new PluginApplicationException(
                    $"Failed to parse the response from Gemini API. " +
                    $"The response format might be invalid or incomplete. " +
                    $"Error: {ex.Message}", 
                    ex);
            }
            finally
            {
                counter++;
            }
        }

        result.ErrorMessages = errorMessages;
        var units = content.GetUnits().ToList();
        result.TotalSegmentsCount = units.SelectMany(x => x.Segments).Count();
        
        units = units.Where(x => x.IsInitial).ToList();
        result.TotalTranslatable = units.SelectMany(x => x.Segments).Count();
        var processedBatches = await units.Batch(batchSize).Process(BatchTranslate);
        result.ProcessedBatchesCount = counter - 1;

        var updatedCount = 0;
        foreach (var (unit, translations) in processedBatches)
        {
            foreach (var (segment, translation) in translations)
            {
                var shouldTranslateFromState = segment.State == null || segment.State == SegmentState.Initial;
                if (!shouldTranslateFromState || string.IsNullOrEmpty(translation))
                {
                    continue;
                }

                if (segment.GetTarget() != translation)
                {
                    updatedCount++;
                    segment.SetTarget(translation);
                    segment.State = SegmentState.Translated;
                }
            }
            
            unit.Provenance.Translation.Tool = model;
        }

        result.TargetsUpdatedCount = updatedCount;

        if (input.OutputFileHandling == "original")
        {
            var targetContentLoadResult = content.Target();
            if (!targetContentLoadResult.Success)
            {
                throw new PluginMisconfigurationException(
                    loadResult.Error ?? "An unknown error occured while parsing the content");
            }
            
            var targetContent = targetContentLoadResult.Value;
            result.File = await fileManagementClient.UploadAsync(targetContent.ToStream(), targetContent.OriginalMediaType, targetContent.OriginalName);
        } 
        else
        {
            result.File = await fileManagementClient.UploadAsync(
                content.Serialize().ToStream(), 
                MediaTypes.Xliff2, 
                input.File.Name);
        }

        return result;
    }

    [Action("Translate in background", Description = "Translate file content retrieved from a CMS or file storage. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<StartBatchResponse> BatchTranslateContent(
        [ActionParameter] TranslateFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary, 
        [ActionParameter] BucketRequest bucketRequest)
    {
        var model = input.AIModel;
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var contentLoadResult = Transformation.Load(stream, input.File.Name);
        if (!contentLoadResult.Success)
            throw new PluginMisconfigurationException(contentLoadResult.Error);

        var content = contentLoadResult.Value;
        content.SourceLanguage ??= input.SourceLanguage;
        content.TargetLanguage ??= input.TargetLanguage;
        if (content.TargetLanguage == null) 
            throw new PluginMisconfigurationException(
                "The target language is not defined yet. Please assign the target language in this action.");

        var plainTextLoadResult = content.Source();
        if (!plainTextLoadResult.Success)
        {
            throw new PluginMisconfigurationException(
                plainTextLoadResult.Error ?? "An unknown error occured while parsing the content");
        }

        var plainText = plainTextLoadResult.Value.GetPlaintext();
        content.SourceLanguage ??= await IdentifySourceLanguage(promptRequest, model, plainText);

        var segmentList = content.GetUnits().SelectMany(x => x.Segments).GetSegmentsForTranslation().ToList();

        var glossaryStructure = await GlossaryHelper.ParseGlossary(fileManagementClient, glossary?.Glossary);

        var jsonlMs = new MemoryStream();
        await using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            var bucketSize = bucketRequest.GetBucketSizeOrDefault();

            for (int i = 0; i < segmentList.Count; i += bucketSize)
            {
                var bucketSegments = segmentList.Skip(i).Take(bucketSize).ToList();
                var inputObjects = bucketSegments.Select((s, index) => new 
                { 
                    id = i + index,
                    text = s.GetSource() 
                });
                var segmentsJson = JsonConvert.SerializeObject(inputObjects);

                var userPrompt = new StringBuilder();
                userPrompt.AppendLine(
                    $"Translate the following texts from {content.SourceLanguage} to {content.TargetLanguage}. " +
                    $"Return the translations in the specified JSON schema structure. " +
                    $"Do not skip any entries or provide partial results. {prompt}");
                userPrompt.AppendLine();
                userPrompt.AppendLine($"Original texts: {segmentsJson}");

                if (glossaryStructure != null)
                {
                    var combinedText = string.Join(" ", bucketSegments.Select(s => s.GetSource()));
                    var glossaryPromptPart = GlossaryHelper.GetGlossaryPromptPart(glossaryStructure, combinedText);

                    if (!string.IsNullOrWhiteSpace(glossaryPromptPart))
                    {
                        userPrompt.AppendLine();
                        userPrompt.AppendLine("Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                                            "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                                            "If a term has variations or synonyms, consider them and choose the most appropriate " +
                                            "translation to maintain consistency and precision.");
                        userPrompt.AppendLine(glossaryPromptPart);
                    }
                }

                var req = BatchHelper.BuildBatchRequestObject(
                    userPrompt.ToString(), 
                    SystemPrompt, 
                    promptRequest, 
                    input.AIModel, 
                    ResponseSchemas.IdTranslationArray);
                
                var line = JsonConvert.SerializeObject(req, Formatting.None);
                await sw.WriteLineAsync(line);
            }
        }

        var job = await CreateBatchRequest(jsonlMs, input.AIModel);
        content.MetaData.Add(new Metadata("background-type", "translate") { Category = [Meta.Categories.Blackbird]});

        return new StartBatchResponse
        {
            JobName = job.Name,
            TransformationFile = await fileManagementClient.UploadAsync(
                content.Serialize().ToStream(),
                MediaTypes.Xliff2, 
                input.File.Name)
        };
    }

    [BlueprintActionDefinition(BlueprintAction.TranslateText)]
    [Action("Translate text", Description = "Localize the text provided.")]
    public async Task<TextTranslationResponse> LocalizeText(
        [ActionParameter] TranslateTextRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary)
    {
        var model = input.AIModel;
        var systemPrompt = "You are a text localizer. Localize the provided text for the specified locale while " +
                           $"preserving the original text structure. Respond with localized text. {prompt}";

        var userPrompt = @$"
                    Original text: {input.Text}
                    Locale: {input.TargetLanguage} 

                    ";

        if (glossary.Glossary != null)
        {
            var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary, input.Text);
            if (glossaryPromptPart != null)
            {
                userPrompt +=
                    "\nEnhance the localized text by incorporating relevant terms from our glossary where applicable. " +
                    "If you encounter terms from the glossary in the text, ensure that the localized text aligns " +
                    "with the glossary entries for the respective languages. If a term has variations or synonyms, " +
                    "consider them and choose the most appropriate translation from the glossary to maintain " +
                    $"consistency and precision. {glossaryPromptPart}";
            }
        }

        userPrompt += "Localized text: ";

        var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);

        return new()
        {
            TranslatedText = response.Trim(),
        };
    }
}
