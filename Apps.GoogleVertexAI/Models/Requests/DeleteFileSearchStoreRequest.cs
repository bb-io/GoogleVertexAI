using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Requests;

public class DeleteFileSearchStoreRequest
{
    [Display("Store name", Description = "The file search store resource name, for example: fileSearchStores/my-store.")]
    public string StoreName { get; set; } = string.Empty;

    [Display("Force", Description = "If true, delete the store together with all indexed documents.")]
    public bool? Force { get; set; }
}
