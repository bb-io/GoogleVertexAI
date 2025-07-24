using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
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

[ActionList("Editing")]
public class EditActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
    [BlueprintActionDefinition(BlueprintAction.EditFile)]
    [Action("Edit", Description = "Edit a translation. This action assumes you have previously translated content in Blackbird through any translation action.")]
    public async Task<FileEditResponse> EditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of source texts to be translated at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        var batchSize = bucketSize ?? 1500;
        var model = input.AIModel;
        var result = new FileEditResponse();
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var content = await Transformation.Parse(stream, input.File.Name);

        var counter = 1;        
        var errorMessages = new List<string>();

        async Task<IEnumerable<string?>> BatchTranslate(IEnumerable<Segment> batch)
        {
            var json = JsonConvert.SerializeObject(batch.Select((x, i) => "{ID:" + i + "}" + x.GetSource()));

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

            var systemPrompt =
                "You are a linguistic expert that should process the following texts according to the given instructions";

            var userPrompt =
                $"Your input consists of sentences in {content.SourceLanguage} language with their translations into {content.TargetLanguage}. " +
                "Review and edit the translated target text as necessary to ensure it is a correct and accurate translation of the source text. " +
                "If you see XML tags in the source also include them in the target text, don't delete or modify them. " +
                "Include only the target texts (updated or not) in the format [ID:X]{target}. " +
                $"Example: [ID:1]{{target1}},[ID:2]{{target2}}. " +
                $"{prompt ?? ""} {glossaryPrompt ?? ""} Sentences: \n" +
                string.Join("\n", batch.Select((x, i) => $"ID: {i}; Source: {x.GetSource()}; Target: {x.GetTarget()}"));

            var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);

            var results = new List<string?>();
            var matches = Regex.Matches(response, @"\[ID:(.+?)\]\{([\s\S]+?)\}(?=,\[|$|,?\n)").Cast<Match>().ToList();
            foreach (var match in matches)
            {
                if (match.Groups[2].Value.Contains("[ID:"))
                    continue;
                results.Add(match.Groups[2].Value);
            }
            counter++;
            return results;
        }

        var segments = content.GetSegments();
        result.TotalSegmentsCount = segments.Count();
        segments = segments.Where(x => !x.IsIgnorbale && x.State == SegmentState.Translated);
        result.TotalSegmentsReviewed = segments.Count();

        var processedBatches = await segments.Batch(batchSize).Process(BatchTranslate);
        result.ProcessedBatchesCount = counter - 1;

        var updatedCount = 0;
        foreach (var (segment, translation) in processedBatches)
        {
            if (translation is not null)
            {
                if (segment.GetTarget() != translation)
                {
                    updatedCount++;
                    segment.SetTarget(translation);
                }
                segment.State = SegmentState.Reviewed;
            }        
        }

        result.TotalSegmentsUpdated = updatedCount;

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

    [BlueprintActionDefinition(BlueprintAction.EditText)]
    [Action("Edit text", Description = "Review translated text and generate an edited version.")]
    public async Task<TextEditResponse> EditText(
        [ActionParameter] EditTextRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary)
    {
        var model = input.AIModel;
        var systemPrompt =
            $"You are receiving a source text{(input.SourceLanguage != null ? $" written in {input.SourceLanguage} " : "")}" +
            $"that was translated into target text{(input.TargetLanguage != null ? $" written in {input.TargetLanguage}" : "")}. " +
            "Review the target text and respond with edits of the target text as necessary. If no edits required, respond with target text. ";

        if (glossary.Glossary != null)
            systemPrompt +=
                " Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                "If a term has variations or synonyms, consider them and choose the most appropriate " +
                "translation to maintain consistency and precision. If the translation already aligns " +
                "with the glossary, no edits are required.";

        if (prompt != null)
            systemPrompt = $"{systemPrompt} {prompt}";

        var userPrompt = @$"
            Source text: 
            {input.SourceText}

            Target text: 
            {input.TargetText}
        ";

        if (glossary.Glossary != null)
        {
            var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, input.SourceText);
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
            EditedText = response.Trim(),
        };
    }
}
