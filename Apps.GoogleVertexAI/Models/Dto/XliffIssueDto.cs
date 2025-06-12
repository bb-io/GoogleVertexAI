using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Dto;

public class XliffIssueDto
{
    [Display("Translation Unit ID")]
    public string Id { get; set; } = string.Empty;
    
    [Display("Source Text")]
    public string Source { get; set; } = string.Empty;
    
    [Display("Target Text")]
    public string Target { get; set; } = string.Empty;
    
    [Display("Issues")]
    public string Issues { get; set; } = string.Empty;
}
