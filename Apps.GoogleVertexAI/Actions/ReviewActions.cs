using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Ai.Files.Models;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Enums;
using SegmentStateSets = Blackbird.Ai.Segments.SegmentStateSets;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Quality estimation")]
public class ReviewActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiContentInvocable(invocationContext, fileManagementClient)
{
    // The action's threshold, average and percentage are on a 0-100 scale; the library scores 0-1.
    private const double ScoreScale = 100;

    [Action("Quality estimation (experimental)", Description = "Evaluates unit and file level translation quality for translated files.")]
    public async Task<ScoreResponse> Score(
        [ActionParameter] AIModelRequest model,
        [ActionParameter] ScoreRequest input,
        [ActionParameter, Display("Prompt", Description = "Add any linguistic criteria for quality evaluation")] string? prompt,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Bucket size", Description = "Specify the maximum number of segments to be estimated at once. Default value: 250. Buckets are also sized to fit the model's output limit. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        if (input.Threshold is < 0 or > ScoreScale)
            throw new PluginMisconfigurationException("Score threshold must be in range 0..100.");

        var newState = SegmentStateHelper.ToSegmentState(input.NewState ?? string.Empty) ?? SegmentState.Reviewed;
        var threshold = input.Threshold / ScoreScale;

        var client = CreateLlmClient(model.AIModel, promptRequest);
        var processor = CreateContentProcessor(client, model.AIModel, bucketSize);

        await using var file = await FileManagementClient.DownloadAsync(input.File);

        var result = await AiAdapters.Mapped(() => processor.ReviewAsync(new ReviewStreamOptions
        {
            File = file,
            FileName = input.File.Name,
            MediaType = input.File.ContentType,
            SourceLanguage = input.SourceLanguage,
            TargetLanguage = input.TargetLanguage,
            AdditionalInstructions = prompt,
            SegmentStates = SegmentStateSets.Parse(input.EstimateUnitsWhereAllSegmentStates, SegmentStateSets.Review),

            // Without a threshold nothing is promoted or counted as under it, as before.
            Threshold = threshold ?? 0.8,
            LowScoreThreshold = threshold ?? 0,
            StateAboveThreshold = threshold is null ? null : newState
        }));

        var content = result.Content;
        var review = content.Review!;

        return new ScoreResponse
        {
            File = await UploadAsync(result.File),
            TotalUnits = content.Content.GetUnits().Count(),
            TotalUnitsProcessed = review.ScoredSegments,
            TotalUnitsUnderThreshhold = threshold is null ? 0 : review.UnderThreshold,
            TotalSegmentsFinalized = threshold is null ? 0 : review.Finalized,
            AverageScore = review.AverageScore * ScoreScale,
            PercentageUnitsUnderThreshold = threshold is null ? 0 : review.PercentageUnderThreshold,
            Usage = ToUsageDto(content.Usage, client),
        };
    }
}
