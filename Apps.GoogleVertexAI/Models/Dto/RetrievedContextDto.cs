using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Dto;

public class RetrievedContextDto
{
    [Display("Title")]
    public string? Title { get; set; }

    [Display("URI")]
    public string? Uri { get; set; }

    [Display("Text")]
    public string? Text { get; set; }

    [Display("File search store")]
    public string? FileSearchStore { get; set; }
}
