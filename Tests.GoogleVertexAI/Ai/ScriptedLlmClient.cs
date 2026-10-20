using System.Text.RegularExpressions;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI.Ai;

// Offline stand-in for Gemini: answers every item of the library's prompt through a function and records
// each request.
public sealed class ScriptedLlmClient(Func<LlmRequest, string> answer) : ILlmClient
{
    private static readonly Regex ItemId = new("\"id\":\"([^\"]+)\"");

    public List<LlmRequest> Requests { get; } = [];

    public LlmCapabilities Capabilities { get; } = new(SupportsStructuredOutput: true, ContextTokens: 1_048_576, MaxOutputTokens: 65_535);

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        return Task.FromResult(LlmResponse.Completed(answer(request), new LlmUsage(10, 5)));
    }

    public static ScriptedLlmClient Unusable() => new(_ => "not json");

    public static ScriptedLlmClient Never() =>
        new(_ => throw new InvalidOperationException("The model must not be called."));

    // Answers every item with the same text.
    public static ScriptedLlmClient Texts(string text) => new(request => Envelope(request, "translated_text", text));

    // Answers every item with the same quality score (0-1).
    public static ScriptedLlmClient Scores(double score) => new(request => Envelope(request, "quality_score", score));

    private static string Envelope(LlmRequest request, string property, object value) =>
        JsonConvert.SerializeObject(new
        {
            translations = ItemId.Matches(request.UserPrompt)
                .Select(match => new Dictionary<string, object>
                {
                    ["translation_id"] = match.Groups[1].Value,
                    [property] = value
                })
                .ToList()
        });
}
