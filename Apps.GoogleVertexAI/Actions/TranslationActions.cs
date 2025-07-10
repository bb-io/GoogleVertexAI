using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Glossaries.Utils.Converters;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Translation")]
public class TranslationActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
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
        var content = await Transformation.Parse(stream, input.File.Name);
        content.SourceLanguage ??= input.SourceLanguage;
        content.TargetLanguage ??= input.TargetLanguage;
        if (content.TargetLanguage == null) throw new PluginMisconfigurationException("The target language is not defined yet. Please assign the target language in this action.");

        if (content.SourceLanguage == null)
        {
            content.SourceLanguage = await IdentifySourceLanguage(promptRequest, model, content.Source().GetPlaintext());
        }

        var counter = 1;        
        var errorMessages = new List<string>();

        async Task<IEnumerable<string?>> BatchTranslate(IEnumerable<Segment> batch)
        {
            var json = JsonConvert.SerializeObject(batch.Select((x, i) => "{ID:" + i + "}" + x.GetSource()));

            var systemPrompt = 
               "You are tasked with localizing the provided text. Consider cultural nuances, idiomatic expressions, " +
               "and locale-specific references to make the text feel natural in the target language. " +
               "Ensure the structure of the original text is preserved. Respond with the localized text." +
               "Please note that each text is considered as an individual item for translation. Even if there are entries " +
               "that are identical or similar, each one should be processed separately. This is crucial because the output " +
               "should be an array with the same number of elements as the input. This array will be used programmatically, " +
               "so maintaining the same element count is essential.";

            var userPrompt = 
                $"Please process ALL texts in the provided array. It is critical that you translate EVERY item individually, not just the first one. " +
                $"Translate the following texts from {content.SourceLanguage} to {content.TargetLanguage}. Return the outputs as a serialized JSON array of strings without additional formatting, " +
                $"maintaining the exact same number of elements as the input array. " +
                $"This is crucial because your response will be deserialized programmatically. " +
                $"Do not skip any entries or provide partial results. {prompt}" +
                $"Original texts (in serialized array format): {json}";

            if (glossary.Glossary != null)
            {
                var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, json);
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

            var results = new List<string?>();

            try
            {
                var result = GeminiResponseParser.ParseStringArray(response, InvocationContext.Logger);
                if (result.IsPartial)
                {
                    errorMessages.Add(
                        $"The response from the Gemini API (batch number: {counter}) was incomplete. " +
                        $"Got {result.Results.Length} results, but expected {batch.Count()}. " +
                        "Try to reduce the batch size.");
                }

                foreach (var item in result.Results)
                {
                    var match = Regex.Match(item, "\\{ID:(.*?)\\}(.+)$");
                    if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        var id = match.Groups[1].Value;
                        var content = match.Groups[2].Value;

                        results.Add(content);
                    } else
                    {
                        results.Add(null);
                    }
                }

                return results;
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

        var segments = content.GetSegments();
        result.TotalSegmentsCount = segments.Count();
        segments = segments.Where(x => !x.IsIgnorbale && x.IsInitial);
        result.TotalTranslatable = segments.Count();

        var processedBatches = await segments.Batch(batchSize).Process(BatchTranslate);
        result.ProcessedBatchesCount = counter - 1;

        var updatedCount = 0;
        foreach (var (segment, translation) in processedBatches)
        {
            if (translation is not null)
            {
                updatedCount++;
                segment.SetTarget(translation);
                segment.State = SegmentState.Translated;
            }        
        }

        result.TargetsUpdatedCount = updatedCount;

        if (input.OutputFileHandling == "original")
        {
            var targetContent = content.Target();
            result.File = await fileManagementClient.UploadAsync(targetContent.Serialize().ToStream(), targetContent.OriginalMediaType, targetContent.OriginalName);
        } else
        {
            result.File = await fileManagementClient.UploadAsync(content.Serialize().ToStream(), MediaTypes.Xliff, content.XliffFileName);
        }        

        return result;

    }

    private async Task<string?> GetGlossaryPromptPart(FileReference glossary, string sourceContent)
    {
        var glossaryStream = await fileManagementClient.DownloadAsync(glossary);
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
            var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, input.Text);
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
