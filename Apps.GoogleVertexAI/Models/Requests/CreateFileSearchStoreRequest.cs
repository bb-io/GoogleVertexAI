using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class CreateFileSearchStoreRequest
{
    [Display("Display name", Description = "Human-readable display name for the file search store."), DataSource(typeof(FileSearchStoreDataSourceHandler))]
    public string? DisplayName { get; set; }
}
