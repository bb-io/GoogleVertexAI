using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Requests;

public class CreateFileSearchStoreRequest
{
    [Display("Display name", Description = "Human-readable display name for the file search store.")]
    public string? DisplayName { get; set; }
}
