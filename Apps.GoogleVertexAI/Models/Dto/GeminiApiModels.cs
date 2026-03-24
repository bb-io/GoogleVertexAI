using System.Text.Json.Serialization;

namespace Apps.GoogleVertexAI.Models.Dto;

public class GeminiFileSearchStoreResource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class GeminiFileSearchStoreListResponse
{
    [JsonPropertyName("fileSearchStores")]
    public List<GeminiFileSearchStoreResource>? FileSearchStores { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class GeminiFileSearchOperation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiFileUploadRequest
{
    [JsonPropertyName("file")]
    public GeminiFileMetadata? File { get; set; }
}

public class GeminiFileUploadResponse
{
    [JsonPropertyName("file")]
    public GeminiFileResource? File { get; set; }
}

public class GeminiRegisterFilesRequest
{
    [JsonPropertyName("uris")]
    public List<string> Uris { get; set; } = [];
}

public class GeminiRegisterFilesResponse
{
    [JsonPropertyName("files")]
    public List<GeminiFileResource>? Files { get; set; }
}

public class GeminiFileMetadata
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class GeminiFileResource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiGoogleErrorEnvelope
{
    [JsonPropertyName("error")]
    public GeminiGoogleError? Error { get; set; }
}

public class GeminiGoogleError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class GeminiUploadToFileSearchStoreRequest
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("customMetadata")]
    public List<GeminiCustomMetadata>? CustomMetadata { get; set; }

    [JsonPropertyName("chunkingConfig")]
    public GeminiChunkingConfig? ChunkingConfig { get; set; }
}

public class GeminiImportFileToSearchStoreRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("customMetadata")]
    public List<GeminiCustomMetadata>? CustomMetadata { get; set; }

    [JsonPropertyName("chunkingConfig")]
    public GeminiChunkingConfig? ChunkingConfig { get; set; }
}

public class GeminiCustomMetadata
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("stringValue")]
    public string? StringValue { get; set; }

    [JsonPropertyName("numericValue")]
    public double? NumericValue { get; set; }
}

public class GeminiChunkingConfig
{
    [JsonPropertyName("whiteSpaceConfig")]
    public GeminiWhiteSpaceChunkingConfig? WhiteSpaceConfig { get; set; }
}

public class GeminiWhiteSpaceChunkingConfig
{
    [JsonPropertyName("maxTokensPerChunk")]
    public int? MaxTokensPerChunk { get; set; }

    [JsonPropertyName("maxOverlapTokens")]
    public int? MaxOverlapTokens { get; set; }
}

public class GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig GenerationConfig { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<GeminiTool>? Tools { get; set; }

    [JsonPropertyName("safetySettings")]
    public List<GeminiSafetySetting>? SafetySettings { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = [];
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("topP")]
    public float TopP { get; set; }

    [JsonPropertyName("topK")]
    public int TopK { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }
}

public class GeminiTool
{
    [JsonPropertyName("fileSearch")]
    public GeminiFileSearchTool? FileSearch { get; set; }
}

public class GeminiFileSearchTool
{
    [JsonPropertyName("fileSearchStoreNames")]
    public List<string> FileSearchStoreNames { get; set; } = [];

    [JsonPropertyName("metadataFilter")]
    public string? MetadataFilter { get; set; }
}

public class GeminiSafetySetting
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("threshold")]
    public string Threshold { get; set; } = string.Empty;
}

public class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("groundingMetadata")]
    public GeminiGroundingMetadata? GroundingMetadata { get; set; }
}

public class GeminiGroundingMetadata
{
    [JsonPropertyName("groundingChunks")]
    public List<GeminiGroundingChunk>? GroundingChunks { get; set; }
}

public class GeminiGroundingChunk
{
    [JsonPropertyName("retrievedContext")]
    public GeminiRetrievedContext? RetrievedContext { get; set; }
}

public class GeminiRetrievedContext
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("fileSearchStore")]
    public string? FileSearchStore { get; set; }
}

public class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}
