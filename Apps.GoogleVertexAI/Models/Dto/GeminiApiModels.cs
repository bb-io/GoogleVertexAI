using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Models.Dto;

public class GeminiFileSearchStoreResource
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
}

public class GeminiFileSearchStoreListResponse
{
    [JsonProperty("fileSearchStores")]
    public List<GeminiFileSearchStoreResource>? FileSearchStores { get; set; }

    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class GeminiFileSearchOperation
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("done")]
    public bool Done { get; set; }

    [JsonProperty("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiFileUploadRequest
{
    [JsonProperty("file")]
    public GeminiFileMetadata? File { get; set; }
}

public class GeminiFileUploadResponse
{
    [JsonProperty("file")]
    public GeminiFileResource? File { get; set; }
}

public class GeminiRegisterFilesRequest
{
    [JsonProperty("uris")]
    public List<string> Uris { get; set; } = [];
}

public class GeminiRegisterFilesResponse
{
    [JsonProperty("files")]
    public List<GeminiFileResource>? Files { get; set; }
}

public class GeminiFileMetadata
{
    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
}

public class GeminiFileResource
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("state")]
    public string? State { get; set; }

    [JsonProperty("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiGoogleErrorEnvelope
{
    [JsonProperty("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiGoogleError
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}

public class GeminiUploadToFileSearchStoreRequest
{
    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("mimeType")]
    public string? MimeType { get; set; }

    [JsonProperty("customMetadata")]
    public List<GeminiCustomMetadata>? CustomMetadata { get; set; }

    [JsonProperty("chunkingConfig")]
    public GeminiChunkingConfig? ChunkingConfig { get; set; }
}

public class GeminiImportFileToSearchStoreRequest
{
    [JsonProperty("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("customMetadata")]
    public List<GeminiCustomMetadata>? CustomMetadata { get; set; }

    [JsonProperty("chunkingConfig")]
    public GeminiChunkingConfig? ChunkingConfig { get; set; }
}

public class GeminiCustomMetadata
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("stringValue")]
    public string? StringValue { get; set; }

    [JsonProperty("numericValue")]
    public double? NumericValue { get; set; }
}

public class GeminiChunkingConfig
{
    [JsonProperty("whiteSpaceConfig")]
    public GeminiWhiteSpaceChunkingConfig? WhiteSpaceConfig { get; set; }
}

public class GeminiWhiteSpaceChunkingConfig
{
    [JsonProperty("maxTokensPerChunk")]
    public int? MaxTokensPerChunk { get; set; }

    [JsonProperty("maxOverlapTokens")]
    public int? MaxOverlapTokens { get; set; }
}

public class GeminiGenerateContentRequest
{
    [JsonProperty("contents")]
    public List<GeminiContent> Contents { get; set; } = [];

    [JsonProperty("generationConfig")]
    public GeminiGenerationConfig GenerationConfig { get; set; } = new();

    [JsonProperty("tools")]
    public List<GeminiTool>? Tools { get; set; }

    [JsonProperty("safetySettings")]
    public List<GeminiSafetySetting>? SafetySettings { get; set; }
}

public class GeminiContent
{
    [JsonProperty("parts")]
    public List<GeminiPart> Parts { get; set; } = [];
}

public class GeminiPart
{
    [JsonProperty("text")]
    public string? Text { get; set; }
}

public class GeminiGenerationConfig
{
    [JsonProperty("temperature")]
    public float Temperature { get; set; }

    [JsonProperty("topP")]
    public float TopP { get; set; }

    [JsonProperty("topK")]
    public int TopK { get; set; }

    [JsonProperty("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }
}

public class GeminiTool
{
    [JsonProperty("fileSearch")]
    public GeminiFileSearchTool? FileSearch { get; set; }
}

public class GeminiFileSearchTool
{
    [JsonProperty("fileSearchStoreNames")]
    public List<string> FileSearchStoreNames { get; set; } = [];

    [JsonProperty("metadataFilter")]
    public string? MetadataFilter { get; set; }
}

public class GeminiSafetySetting
{
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("threshold")]
    public string Threshold { get; set; } = string.Empty;
}

public class GeminiGenerateContentResponse
{
    [JsonProperty("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonProperty("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiCandidate
{
    [JsonProperty("content")]
    public GeminiContent? Content { get; set; }

    [JsonProperty("groundingMetadata")]
    public GeminiGroundingMetadata? GroundingMetadata { get; set; }
}

public class GeminiGroundingMetadata
{
    [JsonProperty("groundingChunks")]
    public List<GeminiGroundingChunk>? GroundingChunks { get; set; }
}

public class GeminiGroundingChunk
{
    [JsonProperty("retrievedContext")]
    public GeminiRetrievedContext? RetrievedContext { get; set; }
}

public class GeminiRetrievedContext
{
    [JsonProperty("uri")]
    public string? Uri { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("fileSearchStore")]
    public string? FileSearchStore { get; set; }
}

public class GeminiUsageMetadata
{
    [JsonProperty("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonProperty("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonProperty("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}
