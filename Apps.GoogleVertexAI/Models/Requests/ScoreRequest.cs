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

    [Display("Score threshold", Description = "Scores range from 0 (lowest) to 100 (highest confidence). Segments scoring at or above the threshold get the new segment state; anything lower fails the quality check. Default value: 80.")]
    public double? Threshold { get; set; }

    [Display("New segment state", Description = "The target segment state to assign when the threshold condition is met ('review' by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public string? NewState { get; set; }

    [Display("Segment states to estimate", Description = "Only segments in the selected states are estimated ('translated' and 'reviewed' states by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public IEnumerable<string>? EstimateUnitsWhereAllSegmentStates { get; set; }
}