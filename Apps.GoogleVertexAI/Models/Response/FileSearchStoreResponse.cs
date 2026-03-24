using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response;

public class FileSearchStoreResponse
{
    [Display("Store name")]
    public string StoreName { get; set; } = string.Empty;

    [Display("Display name")]
    public string? DisplayName { get; set; }
}
