using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Models.Dto;

public class GeminiModelListResponse
{
    [JsonProperty("models")]
    public List<GeminiApiModelResource>? Models { get; set; }

    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class GeminiApiModelResource
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("supportedGenerationMethods")]
    public List<string>? SupportedGenerationMethods { get; set; }
}

public class VertexPublisherModelListResponse
{
    [JsonProperty("publisherModels")]
    public List<VertexPublisherModelResource>? PublisherModels { get; set; }

    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class VertexPublisherModelResource
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
}
