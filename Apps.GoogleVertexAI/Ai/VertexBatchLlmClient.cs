using System.Runtime.CompilerServices;
using System.Text;
using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apps.GoogleVertexAI.Ai;

// Vertex batch prediction behind the library's batch interface. Every line carries the request a direct
// call sends; its bucket id travels in the request labels, which Vertex echoes in the output line.
public sealed class VertexBatchLlmClient(
    IEnumerable<AuthenticationCredentialsProvider> credentials,
    string projectId,
    string region,
    string? model,
    PromptRequest settings) : IBatchLlmClient
{
    internal const string RequestIdLabel = "blackbird_request_id";

    private const string InputFileName = "input.jsonl";

    private static readonly JsonParser ResponseParser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    private readonly object _gate = new();
    private UsageDto _usage = new();
    private LlmUsage _reportedUsage;

    public LlmCapabilities Capabilities { get; } = GeminiLlmClient.CapabilitiesFor(model, settings.MaxOutputTokens);

    // Gemini's own usage of the result lines read so far (its total includes thinking tokens).
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

    // The same lines as the library sees them, so the preparation usage at start can be told apart.
    public LlmUsage ReportedUsage
    {
        get
        {
            lock (_gate)
            {
                return _reportedUsage;
            }
        }
    }

    public async Task<BatchJob> SubmitAsync(IReadOnlyList<BatchLlmRequest> requests, CancellationToken cancellationToken)
    {
        var modelId = string.IsNullOrWhiteSpace(model)
            ? throw new PluginMisconfigurationException("Model is required.")
            : model;

        using var jsonl = new MemoryStream();
        await using (var writer = new StreamWriter(jsonl, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            foreach (var request in requests)
            {
                await writer.WriteLineAsync(ToLine(request, modelId, settings));
            }
        }

        return ToJob(await VertexBatchJobs.CreateAsync(credentials, projectId, region, jsonl, modelId));
    }

    public async Task<BatchJob> GetJobAsync(string jobId, CancellationToken cancellationToken) =>
        ToJob(await VertexBatchJobs.GetAsync(credentials, jobId));

    // Vertex writes successful and failed rows to the same JSONL files in the job's output directory.
    public async IAsyncEnumerable<BatchLlmResult> GetResultsAsync(
        BatchJob job,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = await VertexBatchJobs.GetAsync(credentials, job.Id);
        var outputDirectory = batch.OutputInfo?.GcsOutputDirectory is { Length: > 0 } directory
            ? directory
            : batch.OutputConfig?.GcsDestination?.OutputUriPrefix
              ?? throw new PluginApplicationException("Batch job has no GCS output prefix.");

        var (bucket, prefix) = VertexBatchJobs.ParseGcsUri(outputDirectory);
        var storage = ClientFactory.CreateStorage(credentials);
        var objects = storage.ListObjects(bucket, prefix)
            .Where(item => item.Name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                           && !item.Name.EndsWith(InputFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name)
            .ToList();

        foreach (var item in objects)
        {
            using var buffer = new MemoryStream();
            await storage.DownloadObjectAsync(item, buffer, cancellationToken: cancellationToken);
            buffer.Position = 0;

            using var reader = new StreamReader(buffer);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line) && ToResult(line) is { } result)
                {
                    yield return result;
                }
            }
        }
    }

    public static BatchJobState StateOf(JobState state) => state switch
    {
        JobState.Succeeded or JobState.PartiallySucceeded => BatchJobState.Completed,
        JobState.Failed => BatchJobState.Failed,
        JobState.Cancelled => BatchJobState.Cancelled,
        JobState.Expired => BatchJobState.Expired,
        _ => BatchJobState.Running
    };

    internal static string ToLine(BatchLlmRequest request, string modelId, PromptRequest settings)
    {
        // The job names the model, so the request itself carries none.
        var body = VertexGenerativeModelClient.BuildRequest(
            settings,
            string.Empty,
            modelId,
            request.Request.UserPrompt,
            request.Request.SystemPrompt,
            request.Request.ResponseSchema is { } schema ? JObject.Parse(schema.Schema) : null);
        body.Labels[RequestIdLabel] = request.CustomId;

        // Compacted, so every request is exactly one JSONL line.
        return new JObject { ["request"] = JObject.Parse(JsonFormatter.Default.Format(body)) }.ToString(Formatting.None);
    }

    // A row without the label is not one of ours (or the echoed input) and is skipped; a row without a
    // response failed, and its status carries Vertex's reason.
    internal BatchLlmResult? ToResult(string line)
    {
        var row = JObject.Parse(line);
        var customId = row.SelectToken($"request.labels.{RequestIdLabel}")?.ToString();

        if (string.IsNullOrEmpty(customId))
        {
            return null;
        }

        if (row["response"] is not JObject response)
        {
            var status = row["status"]?.ToString();
            return BatchLlmResult.Failed(customId, string.IsNullOrWhiteSpace(status) ? "The request returned no response." : status);
        }

        GenerateContentResponse parsed;
        try
        {
            parsed = ResponseParser.Parse<GenerateContentResponse>(response.ToString(Formatting.None));
        }
        catch (InvalidProtocolBufferException exception)
        {
            return BatchLlmResult.Failed(customId, $"The result could not be read: {exception.Message}");
        }

        var generated = VertexGenerativeModelClient.ToGeneratedText([parsed]);
        var llmResponse = GeminiLlmClient.ToLlmResponse(generated);

        lock (_gate)
        {
            _usage += generated.Usage;
            _reportedUsage += llmResponse.Usage;
        }

        return BatchLlmResult.Succeeded(customId, llmResponse);
    }

    private static BatchJob ToJob(BatchPredictionJob job) => new(
        job.Name,
        StateOf(job.State),
        (int)((job.CompletionStats?.SuccessfulCount ?? 0) + (job.CompletionStats?.FailedCount ?? 0) + (job.CompletionStats?.IncompleteCount ?? 0)),
        (int)(job.CompletionStats?.SuccessfulCount ?? 0),
        (int)(job.CompletionStats?.FailedCount ?? 0),
        string.IsNullOrWhiteSpace(job.Error?.Message) ? null : job.Error.Message,
        job.CreateTime?.ToDateTimeOffset());
}
