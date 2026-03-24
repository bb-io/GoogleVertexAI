using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class SearchDocumentsRequest : PromptRequest
{
    [Display("Query")]
    public string Query { get; set; } = string.Empty;

    [Display("Model")]
    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    public required string AIModel { get; set; }

    [Display("File search store names", Description = "One or more file search store resource names, for example: fileSearchStores/my-store.")]
    [DataSource(typeof(FileSearchStoreDataSourceHandler))]
    public IEnumerable<string> FileSearchStoreNames { get; set; } = [];

    [Display("Metadata filter", Description = "Optional AIP-160 filter expression used to restrict File Search results.")]
    public string? MetadataFilter { get; set; }
}
