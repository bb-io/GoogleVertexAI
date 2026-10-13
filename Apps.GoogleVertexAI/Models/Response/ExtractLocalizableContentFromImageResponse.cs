using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class ExtractLocalizableContentFromImageResponse
{
    public required FileReference File { get; set; }

    [Display("Source language")]
    public string? SourceLanguage { get; set; }

    [Display("Extracted segments count")]
    public int SegmentsCount { get; set; }

    public UsageDto Usage { get; set; } = new();
}
