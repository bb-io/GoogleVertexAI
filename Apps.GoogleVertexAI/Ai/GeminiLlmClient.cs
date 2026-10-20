using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Newtonsoft.Json.Linq;

namespace Apps.GoogleVertexAI.Ai;

// One client for both connections: IGenerativeModelClient is Vertex (service account) or the Gemini API (API key).
public sealed class GeminiLlmClient(IGenerativeModelClient client, string model, PromptRequest settings) : ILlmClient
{
    // Every Gemini 2.x and 3.x model takes about one million input tokens; anything else (custom endpoints,
    // tuned models) gets a conservative value. Buckets are bounded by the output limit long before this.
    private const int GeminiContextTokens = 1_048_576;
    private const int DefaultContextTokens = 128_000;

    private static readonly HashSet<string> ContentFilterReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "SAFETY", "RECITATION", "BLOCKLIST", "PROHIBITEDCONTENT", "SPII", "IMAGESAFETY"
    };

    private readonly object _gate = new();
    private UsageDto _usage = new();

    public LlmCapabilities Capabilities { get; } = CapabilitiesFor(model, settings.MaxOutputTokens);

    // LlmUsage carries only input and output tokens; this keeps Gemini's total, which includes thinking tokens.
    public UsageDto Usage
    {
        get
        {
            lock (_gate)
            {
                return _usage;
            }
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var generated = await client.GenerateAsync(
            settings,
            model,
            request.UserPrompt,
            request.SystemPrompt,
            responseJsonSchema: request.ResponseSchema is { } schema ? JObject.Parse(schema.Schema) : null,
            cancellationToken: cancellationToken);

        lock (_gate)
        {
            _usage += generated.Usage;
        }

        return ToLlmResponse(generated);
    }

    // Shared with VertexBatchLlmClient, so a batch result line is read exactly like a direct answer.
    internal static LlmResponse ToLlmResponse(GeneratedTextDto generated) => new(
        generated.Text,
        new LlmUsage(generated.Usage.PromptTokens, generated.Usage.CandidatesTokens),
        MapFinishReason(generated.FinishReason));

    internal static FinishReason MapFinishReason(string? finishReason)
    {
        if (string.IsNullOrWhiteSpace(finishReason))
        {
            return FinishReason.Stop;
        }

        // The REST API reports "MAX_TOKENS", the Vertex SDK enum is normalised to the same value; other
        // Vertex values arrive as enum names such as "Safety".
        var normalized = finishReason.Replace("_", string.Empty);

        if (string.Equals(finishReason, GenerativeModelClientBase.MaxTokensFinishReason, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "MaxTokens", StringComparison.OrdinalIgnoreCase))
        {
            return FinishReason.Truncated;
        }

        if (string.Equals(normalized, "STOP", StringComparison.OrdinalIgnoreCase))
        {
            return FinishReason.Stop;
        }

        return ContentFilterReasons.Contains(normalized) ? FinishReason.ContentFilter : FinishReason.Other;
    }

    internal static LlmCapabilities CapabilitiesFor(string? model, int? requestedMaxOutputTokens)
    {
        var maxOutputTokens = ModelTokenService.GetMaxTokensForModel(model);
        var contextTokens = model?.Trim().StartsWith("gemini-", StringComparison.OrdinalIgnoreCase) == true
            ? GeminiContextTokens
            : DefaultContextTokens;

        return new LlmCapabilities(
            SupportsStructuredOutput: true,
            ContextTokens: contextTokens,
            MaxOutputTokens: requestedMaxOutputTokens is > 0
                ? Math.Min(requestedMaxOutputTokens.Value, maxOutputTokens)
                : maxOutputTokens);
    }
}
