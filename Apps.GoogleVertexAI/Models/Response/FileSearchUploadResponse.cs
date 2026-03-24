using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;

public class FileSearchUploadResponse
{
    [Display("Store name")]
    public string StoreName { get; set; } = string.Empty;

    [Display("Operation name")]
    public string OperationName { get; set; } = string.Empty;

    [Display("Display name")]
    public string? DisplayName { get; set; }
}
