using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;
public class BatchResult
{
    [Display("Result")]
    public string Result { get; set; }

    public UsageDto Usage { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
