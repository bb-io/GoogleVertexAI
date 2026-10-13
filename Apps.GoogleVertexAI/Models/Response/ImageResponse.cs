using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class ImageResponse
{
    public required FileReference Image { get; set; }

    [Display("Usage")]
    public UsageDto Usage { get; set; } = new();
}
