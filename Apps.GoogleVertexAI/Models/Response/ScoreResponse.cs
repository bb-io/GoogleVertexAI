using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class ScoreResponse
{
    public FileReference File { get; set; } = new();

    [Display("Total units")]
    public int TotalUnits { get; set; }

    [Display("Total units processed")]
    public int TotalUnitsProcessed { get; set; }

    [Display("Total units under threshold")]
    public int TotalUnitsUnderThreshhold { get; set; }

    [Display("Total segments finalized (unit can have multiple segments)")]
    public int TotalSegmentsFinalized { get; set; }

    [Display("Average score")]
    public double AverageScore { get; set; }

    [Display("Percentage units under threshold")]
    public double PercentageUnitsUnderThreshold { get; set; }

    [Display("Usage")]
    public UsageDto Usage { get; set; } = new();
}