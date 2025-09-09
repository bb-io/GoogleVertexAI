using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ScoreRequest
{
    public FileReference File { get; set; } = new();

    [Display("Source language")]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    public string? TargetLanguage { get; set; }

    [Display("Score threshold", Description = "Scores range from 0 (lowest) to 100 (highest confidence).")]
    public float? Threshold { get; set; }

    [Display("Threshold comparison", Description = "Determines how the score will be compared to the threshold (by default segment score should be greater or equal the threshold).")]
    [StaticDataSource(typeof(ConditionDataSourceHandler))]
    public string? ScoreThresholdComparison { get; set; }

    [Display("New segment state", Description = "The target segment state to assign when the threshold condition is met (won't be changed by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public string? NewState { get; set; }

    [Display("Segment states to estimate", Description = "Only XLIFF segments in the selected states will be included in estimation ('initial' and 'translated' states by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public IEnumerable<string>? SegmentStatesToEstimate { get; set; }

    [Display("Save scores in segments")]
    public bool? SaveScores { get; set; }
}