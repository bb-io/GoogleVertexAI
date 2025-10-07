using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
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

        async Task<IEnumerable<string?>> BatchEdit(IEnumerable<Segment> batch)
        {
            var json = JsonConvert.SerializeObject(batch.Select((x, i) => "{ID:" + i + "}" + x.GetSource()));

            string? glossaryPrompt = null;
            if (glossary?.Glossary != null)
            {
                var glossaryPromptPart =
                    await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary,
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

        var processedBatches = await segments.Batch(batchSize).Process(BatchEdit);
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

    [Action("Edit in background", Description = "Edit file content retrieved from a CMS or file storage. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<StartBatchResponse> BatchEditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] BucketRequest bucketRequest)
    {
        var model = input.AIModel;
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var content = await Transformation.Parse(stream, input.File.Name);

        var segments = content.GetSegments();
        segments = segments.Where(x => !x.IsIgnorbale && x.IsInitial);

        var glossaryStructure = await GlossaryHelper.ParseGlossary(fileManagementClient, glossary?.Glossary);

        var systemPrompt = "You are a linguistic expert that should process the following texts according to the given instructions";

        var jsonlMs = new MemoryStream();
        using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            // Get bucket size from bucketRequest
            var bucketSize = bucketRequest.GetBucketSizeOrDefault();
            
            // Group segments into buckets
            var segmentList = segments.ToList();
            var bucketCount = 0;
            
            for (int i = 0; i < segmentList.Count; i += bucketSize)
            {
                var bucketSegments = segmentList.Skip(i).Take(bucketSize).ToList();
                var userPrompt = new StringBuilder();
                
                userPrompt.AppendLine($"Your input consists of sentences in {content.SourceLanguage} language with their translations into {content.TargetLanguage}. " +
                    "Review and edit the translated target text as necessary to ensure it is a correct and accurate translation of the source text. " +
                    "If you see XML tags in the source also include them in the target text, don't delete or modify them. " +
                    "Include only the target texts (updated or not) in the format [ID:X]{target}. " +
                    $"Example: [ID:1]{{target1}},[ID:2]{{target2}}. {prompt}");
                userPrompt.AppendLine();
                
                userPrompt.AppendLine("Please process ALL texts in the provided list. It is critical that you edit EVERY item individually.");
                userPrompt.AppendLine("Sentences:");
                
                for (int j = 0; j < bucketSegments.Count; j++)
                {
                    var globalIndex = i + j;
                    var sourceText = bucketSegments[j].GetSource();
                    var targetText = bucketSegments[j].GetTarget();
                    userPrompt.AppendLine($"ID: {globalIndex}; Source: {sourceText}; Target: {targetText}");
                }
                
                // Add glossary if available
                if (glossaryStructure != null)
                {
                    var combinedText = string.Join(" ", bucketSegments.Select(s => s.GetSource())) + " " +
                                       string.Join(" ", bucketSegments.Select(s => s.GetTarget()));
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
                
                var req = BatchHelper.BuildBatchRequestObject(userPrompt.ToString(), systemPrompt, promptRequest, input.AIModel);
                var line = JsonConvert.SerializeObject(req, Formatting.None);
                await sw.WriteLineAsync(line);
                bucketCount++;
            }
        }

        var job = await CreateBatchRequest(jsonlMs, input.AIModel);
        content.MetaData.Add(new Metadata("background-type", "edit") { Category = [Meta.Categories.Blackbird] });

        return new StartBatchResponse
        {
            JobName = job.Name,
            TransformationFile = await fileManagementClient.UploadAsync(content.Serialize().ToStream(), MediaTypes.Xliff, content.XliffFileName)
        };
    }

    private static string BuildPerUnitPostEditPrompt(string id, string source, string target, string? sourceLang, string? targetLang, string? userInstruction, string? glossaryText)
    {
        var sb = new StringBuilder();

        if (sourceLang is not null && targetLang is not null)
        {
            sb.AppendLine($"Your input contains a source sentence in {sourceLang} and its translation into {targetLang}.");
        }
        
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
            var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary, input.SourceText);
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
