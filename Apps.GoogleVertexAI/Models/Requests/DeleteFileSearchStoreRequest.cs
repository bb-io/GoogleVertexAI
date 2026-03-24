using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class DeleteFileSearchStoreRequest
{
    [Display("Store name", Description = "The file search store resource name, for example: fileSearchStores/my-store."), DataSource(typeof(FileSearchStoreDataSourceHandler))]
    public string StoreName { get; set; } = string.Empty;

    [Display("Force", Description = "If true, delete the store together with all indexed documents.")]
    public bool? Force { get; set; }
}
