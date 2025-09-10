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

    [Display("Score threshold", Description = "Scores range from 0 (lowest) to 100 (highest confidence), anything lower than threshlod fails the quality check low confidence.")]
    public double? Threshold { get; set; }

    [Display("New segment state", Description = "The target segment state to assign when the threshold condition is met ('review' by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public string? NewState { get; set; }

    [Display("Segment states to estimate", Description = "Only units with at least one segment in the selected states will be included in estimation ('initial' and 'translated' states by default).")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public IEnumerable<string>? EstimateUnitsWhereAllSegmentStates { get; set; }

    [Display("Save scores in segments", Description = "Saves quality score and treshhold on unit level according to ITS standard.")]
    public bool? SaveScores { get; set; }
}