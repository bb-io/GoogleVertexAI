using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class TranslateXliffResponse
{
    [Display("Tranlated file")]
    public FileReference File { get; set; } = new();
    public UsageDto Usage { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}