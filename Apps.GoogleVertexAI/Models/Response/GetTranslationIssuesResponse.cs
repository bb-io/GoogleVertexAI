using System.Collections.Generic;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;

public class GetTranslationIssuesResponse
{
    [Display("Issues description")]
    public string Issues { get; set; } = string.Empty;
    
    [Display("Translation issues")]
    public List<XliffIssueDto> TranslationIssues { get; set; } = new();
    
    public UsageDto Usage { get; set; } = new();
}
