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
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default);
}
