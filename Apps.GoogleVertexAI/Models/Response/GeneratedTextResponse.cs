using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;

public class GeneratedTextResponse
{
    [Display("Generated text")]
    public string GeneratedText { get; set; } = string.Empty;

    public UsageDto Usage { get; set; } = new();
}