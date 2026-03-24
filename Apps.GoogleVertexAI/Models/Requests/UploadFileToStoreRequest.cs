using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class UploadFileToStoreRequest
{
    [Display("Store name", Description = "The file search store resource name, for example: fileSearchStores/my-store."), DataSource(typeof(FileSearchStoreDataSourceHandler))]
    public string StoreName { get; set; } = string.Empty;

    [Display("File")]
    public FileReference File { get; set; } = new();

    [Display("Display name", Description = "Optional display name for the indexed document.")]
    public string? DisplayName { get; set; }

    [Display("Custom metadata", Description = "Optional metadata entries in key=value format. Numeric values will be sent as numbers when possible.")]
    public IEnumerable<string>? CustomMetadata { get; set; }

    [Display("Max tokens per chunk", Description = "Optional maximum tokens per chunk for whitespace chunking.")]
    public int? MaxTokensPerChunk { get; set; }

    [Display("Max overlap tokens", Description = "Optional maximum overlap tokens for whitespace chunking.")]
    public int? MaxOverlapTokens { get; set; }
}
