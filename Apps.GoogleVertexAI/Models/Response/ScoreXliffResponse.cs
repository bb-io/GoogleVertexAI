using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class ScoreXliffResponse
{
    public FileReference File { get; set; } = new();

    [Display("Average Score")]
    public float AverageScore { get; set; }

    [Display("Usage")]
    public UsageDto Usage { get; set; } = new();
}