using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI.Ai;

// Offline stand-in for Vertex batch prediction: records what start submits, reports a settable state and
// answers every item of every request with the same text.
public sealed class ScriptedBatchLlmClient(string answer) : IBatchLlmClient
{
    public const string JobId = "projects/p/locations/us-central1/batchPredictionJobs/1";

    private static readonly Regex ItemId = new("\"id\":\"([^\"]+)\"");

    public List<BatchLlmRequest> Submitted { get; } = [];

    public BatchJobState State { get; set; } = BatchJobState.Running;

    public LlmCapabilities Capabilities { get; } = new(SupportsStructuredOutput: true, ContextTokens: 1_048_576, MaxOutputTokens: 65_535);

    public Task<BatchJob> SubmitAsync(IReadOnlyList<BatchLlmRequest> requests, CancellationToken cancellationToken)
    {
        Submitted.AddRange(requests);
        return Task.FromResult(Job());
    }

    public Task<BatchJob> GetJobAsync(string jobId, CancellationToken cancellationToken) => Task.FromResult(Job());

    public async IAsyncEnumerable<BatchLlmResult> GetResultsAsync(
        BatchJob job,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        foreach (var request in Submitted)
        {
            var envelope = JsonConvert.SerializeObject(new
            {
                translations = ItemId.Matches(request.Request.UserPrompt)
                    .Select(match => new { translation_id = match.Groups[1].Value, translated_text = answer })
                    .ToList()
            });

            yield return BatchLlmResult.Succeeded(request.CustomId, LlmResponse.Completed(envelope, new LlmUsage(20, 10)));
        }
    }

    private BatchJob Job() => new(JobId, State, Submitted.Count, State == BatchJobState.Running ? 0 : Submitted.Count);
}
