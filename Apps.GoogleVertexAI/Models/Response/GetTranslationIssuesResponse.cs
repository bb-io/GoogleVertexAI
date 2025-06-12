using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;

public class GetTranslationIssuesResponse
{
    [Display("Issues description")]
    public string Issues { get; set; } = string.Empty;
    
    public UsageDto Usage { get; set; } = new();
}
