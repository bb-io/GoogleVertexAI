using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Clients.Abstractions;

public interface IGenerativeModelClient
{
    Task ValidateConnectionAsync(CancellationToken cancellationToken);

    Task<(string Result, UsageDto Usage)> GenerateTextAsync(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        object? schema = null,
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default);

    // The full generation result, finish reason included. A responseJsonSchema is a standard JSON Schema
    // document (Gemini's responseJsonSchema), while schema is Gemini's OpenAPI subset (responseSchema).
    Task<GeneratedTextDto> GenerateAsync(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        object? schema = null,
        object? responseJsonSchema = null,
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default);
}
