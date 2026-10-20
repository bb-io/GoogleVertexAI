using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Transformations;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Ai.Content.Models;
using Blackbird.Ai.Files.Models;
using ShortenPromptStrategy = Blackbird.Ai.Prompts.ShortenPromptStrategy;
using Blackbird.Ai.Summary.Models;
using Blackbird.Ai.Text.Models;
using SegmentStateSets = Blackbird.Ai.Segments.SegmentStateSets;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Editing")]
public class EditActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiContentInvocable(invocationContext, fileManagementClient)
{
    [Action("Shorten content", Description = "Shorten XLIFF target content to fit slr:sizeRestriction values.")]
    public async Task<ShortenContentResponse> ShortenContent(
        [ActionParameter] ShortenContentRequest input,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to apply when shortening.")] string? additionalInstructions,
        [ActionParameter, Display("System prompt (fully replaces shortening instructions)", Description = "Sets the role and context for the model. It is placed in front of the built-in shortening instructions, which keep the response format.")] string? customSystemPrompt,
        [ActionParameter] PromptRequest promptRequest)
    {
        var files = input.Files.ToList();

        if (input.BatchSize is <= 0)
            throw new PluginApplicationException("Batch size must be greater than zero.");
        if (input.RetryCount is < 0)
            throw new PluginApplicationException("Retries must be zero or greater.");

        var results = new ShortenResultDto[files.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (i, _) =>
            {
                var file = files[i];

                try
                {
                    results[i] = await ShortenFile(file, input, additionalInstructions, customSystemPrompt, promptRequest);
                }
                catch (Exception ex)
                {
                    results[i] = new ShortenResultDto
                    {
                        File = file,
                        ErrorMessages = [$"Failed to process '{file.Name}': {ex.Message}"]
                    };
                }
            });

        return new(results);
    }

    [BlueprintActionDefinition(BlueprintAction.EditFile)]
    [Action("Edit",
        Description = "Edit a translation. This action assumes you have previously translated content in Blackbird through any translation action.")]
    public async Task<FileEditResponse> EditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the maximum number of source texts to be edited at once. Default value: 250. Buckets are also sized to fit the model's output limit. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateContentProcessor(client, input.AIModel, bucketSize);

        await using var file = await FileManagementClient.DownloadAsync(input.File);
        await using var glossaryFile = await DownloadOptionalAsync(glossary?.Glossary);

        var result = await AiAdapters.Mapped(() =>
            processor.EditAsync(EditOptions(input, file, glossaryFile, prompt)));

        var content = result.Content;

        return new FileEditResponse
        {
            File = await UploadAsync(result.File),
            TotalSegmentsCount = content.TotalSegments,
            TotalSegmentsReviewed = content.ProcessableSegments,
            TotalSegmentsUpdated = content.UpdatedSegments,
            ProcessedBatchesCount = content.BucketsProcessed,
            ErrorMessages = content.Warnings.ToList()
        };
    }

    [Action("Edit in background", 
        Description = "Edit file content retrieved from a CMS or file storage. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<StartBatchResponse> BatchEditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] BucketRequest bucketRequest)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateBackgroundProcessor(
            CreateBatchClient(input.AIModel, promptRequest),
            client,
            input.AIModel,
            bucketRequest.BucketSize);

        await using var file = await FileManagementClient.DownloadAsync(input.File);
        await using var glossaryFile = await DownloadOptionalAsync(glossary?.Glossary);

        var started = await AiAdapters.Mapped(() =>
            processor.StartEditAsync(EditOptions(input, file, glossaryFile, prompt)));

        return await ToStartBatchResponseAsync(started, client);
    }
        
    [BlueprintActionDefinition(BlueprintAction.EditText)]
    [Action("Edit text", Description = "Review translated text and generate an edited version.")]
    public async Task<TextEditResponse> EditText(
        [ActionParameter] EditTextRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateTextProcessor(client, input.AIModel);

        await using var glossaryFile = await DownloadOptionalAsync(glossary.Glossary);

        var result = await AiAdapters.Mapped(() => processor.EditAsync(new EditTextOptions
        {
            SourceText = input.SourceText,
            TargetText = input.TargetText,
            SourceLanguage = input.SourceLanguage,
            TargetLanguage = input.TargetLanguage,
            AdditionalInstructions = prompt,
            GlossaryTbx = glossaryFile
        }));

        ThrowIfUnresolved(result.Content);

        return new()
        {
            EditedText = result.Text,
        };
    }
    
        // Placeholders written as text, such as {1> and <1}, are plain characters to the library, so its markup
    // check cannot see the model dropping or changing them. Such an answer is rejected, which keeps the
    // previous target and lets the next attempt try again.
    private string? KeepTextPlaceholders(TargetCandidate candidate, ConcurrentQueue<string> errors)
    {
        if (TextPlaceholders(candidate.Segment.GetTarget()).SequenceEqual(TextPlaceholders(candidate.ProposedTarget)))
            return candidate.ProposedTarget;

        var message = $"Gemini changed the tags for unit {candidate.Unit.Id}. Kept the original to avoid broken placeholders.";
        errors.Enqueue(message);
        InvocationContext.Logger?.LogWarning?.Invoke($"[GoogleGemini] {message}", []);
        return null;
    }

    private static List<string> TextPlaceholders(string? text) =>
        TextPlaceholderPattern.Matches(System.Net.WebUtility.HtmlDecode(text ?? string.Empty))
            .Select(match => match.Value)
            .Order()
            .ToList();

    private static readonly Regex TextPlaceholderPattern = new(@"\{\d+>|<\d+[}>]", RegexOptions.Compiled);
    
    private async Task<ShortenResultDto> ShortenFile(
        FileReference file,
        ShortenContentRequest input,
        string? additionalInstructions,
        string? customSystemPrompt,
        PromptRequest promptRequest)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateContentProcessor(client, input.AIModel, input.BatchSize);

        await using var stream = await FileManagementClient.DownloadAsync(file);
        var placeholderErrors = new ConcurrentQueue<string>();

        var result = await AiAdapters.Mapped(() => processor.ShortenAsync(new ShortenStreamOptions
        {
            File = stream,
            FileName = file.Name,
            MediaType = file.ContentType,
            SegmentStates = SegmentStateSets.Parse(input.SegmentStates, SegmentStateSets.Shorten),
            MaxRetries = input.RetryCount ?? ShortenOptions.DefaultMaxRetries,
            AdditionalInstructions = additionalInstructions,
            PromptStrategy = CustomSystemPromptStrategy.For(customSystemPrompt, () => new ShortenPromptStrategy()),
            TransformTarget = candidate => KeepTextPlaceholders(candidate, placeholderErrors)
        }));

        var content = result.Content;
        var shorten = content.Shorten ?? new ShortenSummary(0, 0, 0, 0, 0);
        var units = content.Content.GetUnits().ToList();

        return new ShortenResultDto
        {
            File = await UploadAsync(result.File),
            TotalUnitsCount = units.Count,
            UnitsWithRestrictionCount = units.Count(unit => unit.SizeRestrictions?.MaximumSize is > 0),
            UnitsMatchedFilterCount = shorten.UnitsWithLimit,
            UnitsOverLimitCount = shorten.UnitsOverLimit,
            UnitsUpdatedCount = shorten.UnitsShortened,
            UnitsRemainingOverLimitCount = shorten.UnitsStillOverLimit,
            ProcessedBatchesCount = content.BucketsProcessed,
            RetryAttemptsCount = Math.Max(shorten.Attempts - 1, 0),
            SystemPrompt = content.SystemPrompt ?? string.Empty,
            Usage = ToUsageDto(content.Usage, client),
            ErrorMessages = placeholderErrors.Distinct().Concat(content.Warnings).ToList()
        };
    }
    
    // Shared by "Edit" and "Edit in background", so the two cannot drift apart.
    private static EditStreamOptions EditOptions(
        EditFileRequest input,
        Stream file,
        Stream? glossaryFile,
        string? prompt) => new()
    {
        File = file,
        FileName = input.File.Name,
        MediaType = input.File.ContentType,
        OutputFileHandling = input.OutputFileHandling,
        GlossaryTbx = glossaryFile,
        AdditionalInstructions = prompt,
        Summary = SummaryOptions.FromToggle(input.GenerateDocumentSummary)
    };
}
