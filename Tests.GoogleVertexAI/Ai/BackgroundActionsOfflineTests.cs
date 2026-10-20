using System.Text;
using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using JobState = Google.Cloud.AIPlatform.V1.JobState;

namespace Tests.GoogleVertexAI.Ai;

// Offline, no API spend: start and download run against a scripted batch provider.
[TestClass]
public class BackgroundActionsOfflineTests
{
    private const string Model = "gemini-2.5-flash";

    private const string TwoInitialSegments = """
        <?xml version="1.0" encoding="utf-8"?>
        <xliff xmlns="urn:oasis:names:tc:xliff:document:2.0" version="2.0" srcLang="en" trgLang="de">
          <file id="f1">
            <unit id="u1"><segment id="s1"><source>One</source></segment></unit>
            <unit id="u2"><segment id="s2"><source>Two</source></segment></unit>
          </file>
        </xliff>
        """;

    private const string TwoTranslatedSegments = """
        <?xml version="1.0" encoding="utf-8"?>
        <xliff xmlns="urn:oasis:names:tc:xliff:document:2.0" version="2.0" srcLang="en" trgLang="de">
          <file id="f1">
            <unit id="u1"><segment id="s1" state="translated"><source>Good morning.</source><target>Guten Morgen.</target></segment></unit>
            <unit id="u2"><segment id="s2" state="translated"><source>Thank you.</source><target>Danke.</target></segment></unit>
          </file>
        </xliff>
        """;

    private readonly InMemoryFileManagementClient _files = new();
    private readonly List<string> _loggedWarnings = [];

    [TestMethod]
    public async Task TranslateInBackground_ThenDownload_TranslatesEverySegment()
    {
        var batch = new ScriptedBatchLlmClient("Eins");
        var file = _files.Add("background.xlf", TwoInitialSegments);

        var started = await new ScriptedTranslationActions(Context(), _files, batch).BatchTranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "de", AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest(),
            new BucketRequest());

        Assert.AreEqual(ScriptedBatchLlmClient.JobId, started.JobName);
        Assert.AreEqual(2, started.TotalSegmentsCount);
        Assert.AreEqual(2, started.SegmentsToProcessCount);
        Assert.AreEqual(1, started.RequestsCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(started.SystemPrompt));
        Assert.HasCount(1, batch.Submitted);

        batch.State = BatchJobState.Completed;
        var downloaded = await new ScriptedBatchActions(Context(), _files, batch).DownloadXliffFromBatch(
            null,
            new GetBatchResultRequest { OriginalXliff = started.TransformationFile });

        var segments = Segments(downloaded.File);
        Assert.AreEqual(2, downloaded.UpdatedSegmentsCount);
        Assert.AreEqual(2, downloaded.ProcessedSegmentsCount);
        Assert.AreEqual(0, downloaded.UnresolvedSegmentsCount);
        Assert.AreEqual("completed", downloaded.BatchStatus);
        Assert.AreEqual(30, downloaded.Usage.TotalTokens);
        Assert.IsEmpty(downloaded.Warnings, string.Join(Environment.NewLine, downloaded.Warnings));
        Assert.IsTrue(segments.All(segment => segment.GetTarget() == "Eins" && segment.State == SegmentState.Translated));
    }

    // The work file maps answers by recorded position, so a state change between start and download
    // cannot shift answers onto other segments; edited segments end Reviewed.
    [TestMethod]
    public async Task EditInBackground_ThenDownload_MarksSegmentsReviewed()
    {
        var batch = new ScriptedBatchLlmClient("Hallo.");
        var file = _files.Add("background-edit.xlf", TwoTranslatedSegments);

        var started = await new ScriptedEditActions(Context(), _files, batch).BatchEditContent(
            new EditFileRequest { File = file, AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest(),
            new BucketRequest());

        batch.State = BatchJobState.Completed;
        var downloaded = await new ScriptedBatchActions(Context(), _files, batch).DownloadXliffFromBatch(
            started.JobName,
            new GetBatchResultRequest { OriginalXliff = started.TransformationFile });

        Assert.AreEqual(2, downloaded.UpdatedSegmentsCount);
        Assert.IsTrue(Segments(downloaded.File).All(segment => segment.GetTarget() == "Hallo." && segment.State == SegmentState.Reviewed));
    }

    [TestMethod]
    public async Task DownloadXliffFromBatch_JobStillRunning_ThrowsMisconfiguration()
    {
        var batch = new ScriptedBatchLlmClient("Eins");
        var file = _files.Add("background.xlf", TwoInitialSegments);

        var started = await new ScriptedTranslationActions(Context(), _files, batch).BatchTranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "de", AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest(),
            new BucketRequest());

        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() =>
            new ScriptedBatchActions(Context(), _files, batch).DownloadXliffFromBatch(
                null,
                new GetBatchResultRequest { OriginalXliff = started.TransformationFile }));
    }

    // Old transformation files carry no job; they are not supported any more.
    [TestMethod]
    public async Task DownloadXliffFromBatch_FileWithoutJob_ThrowsMisconfiguration()
    {
        var file = _files.Add("plain.xlf", TwoTranslatedSegments);

        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() =>
            new ScriptedBatchActions(Context(), _files, new ScriptedBatchLlmClient("unused")).DownloadXliffFromBatch(
                "projects/p/locations/us-central1/batchPredictionJobs/9",
                new GetBatchResultRequest { OriginalXliff = file }));
    }

    // Nothing to process: no job is created, the event fires once for the empty job name, and download
    // returns the file unchanged.
    [TestMethod]
    public async Task TranslateInBackground_NothingToProcess_CreatesNoJob()
    {
        var batch = new ScriptedBatchLlmClient("unused");
        var file = _files.Add("done.xlf", TwoTranslatedSegments);

        var started = await new ScriptedTranslationActions(Context(), _files, batch).BatchTranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "de", AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest(),
            new BucketRequest());

        Assert.AreEqual(string.Empty, started.JobName);
        Assert.IsEmpty(batch.Submitted);

        var polling = new BatchPolling(Context());
        var first = await polling.OnBatchFinished(
            new PollingEventRequest<BatchMemory> { Memory = new BatchMemory { LastPollingTime = DateTime.UtcNow } },
            new BatchIdentifier { JobName = started.JobName });
        var second = await polling.OnBatchFinished(
            new PollingEventRequest<BatchMemory> { Memory = first.Memory },
            new BatchIdentifier { JobName = started.JobName });

        Assert.IsTrue(first.FlyBird);
        Assert.IsFalse(second.FlyBird);

        var downloaded = await new ScriptedBatchActions(Context(), _files, batch).DownloadXliffFromBatch(
            null,
            new GetBatchResultRequest { OriginalXliff = started.TransformationFile });

        Assert.AreEqual(0, downloaded.UpdatedSegmentsCount);
        Assert.AreEqual("completed", downloaded.BatchStatus);
        Assert.IsTrue(Segments(downloaded.File).All(segment => segment.State == SegmentState.Translated));
    }

    [TestMethod]
    public void StateOf_MapsEveryTerminalState()
    {
        Assert.AreEqual(BatchJobState.Completed, VertexBatchLlmClient.StateOf(JobState.Succeeded));
        Assert.AreEqual(BatchJobState.Completed, VertexBatchLlmClient.StateOf(JobState.PartiallySucceeded));
        Assert.AreEqual(BatchJobState.Failed, VertexBatchLlmClient.StateOf(JobState.Failed));
        Assert.AreEqual(BatchJobState.Cancelled, VertexBatchLlmClient.StateOf(JobState.Cancelled));
        Assert.AreEqual(BatchJobState.Expired, VertexBatchLlmClient.StateOf(JobState.Expired));
        Assert.AreEqual(BatchJobState.Running, VertexBatchLlmClient.StateOf(JobState.Pending));
        Assert.AreEqual(BatchJobState.Running, VertexBatchLlmClient.StateOf(JobState.Running));
    }

    // A batch line is the request a direct call sends, without the model, with its bucket id in the labels.
    [TestMethod]
    public void ToLine_CarriesTheDirectRequestAndTheBucketId()
    {
        var line = VertexBatchLlmClient.ToLine(
            new BatchLlmRequest("b0", new LlmRequest("system", "user", new JsonSchemaSpec("Test", """{ "type": "object", "additionalProperties": false }"""))),
            Model,
            new PromptRequest { Temperature = 0.2f });

        Assert.Contains("\"labels\":{\"blackbird_request_id\":\"b0\"}", line);
        Assert.Contains("\"systemInstruction\"", line);
        Assert.Contains("\"responseJsonSchema\"", line);
        Assert.Contains("\"additionalProperties\":false", line);
        Assert.Contains("\"temperature\":0.2", line);
        Assert.DoesNotContain("\"model\"", line);
    }

    // A result line is read like a direct answer, truncation included; a failed row keeps Vertex's reason.
    [TestMethod]
    public void ToResult_MapsSuccessTruncationAndFailure()
    {
        var client = new VertexBatchLlmClient([], "p", "us-central1", Model, new PromptRequest());

        var truncated = client.ToResult("""
            {"request":{"labels":{"blackbird_request_id":"b0"}},"status":"","response":{"candidates":[{"content":{"role":"model","parts":[{"text":"{\"translations\":[]"}]},"finishReason":"MAX_TOKENS"}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":20},"modelVersion":"gemini-2.5-flash","someFutureField":1}}
            """)!;
        var failed = client.ToResult("""
            {"request":{"labels":{"blackbird_request_id":"b1"}},"status":"Bad Request: invalid schema"}
            """)!;
        var foreign = client.ToResult("""
            {"request":{"contents":[]},"status":""}
            """);

        Assert.AreEqual("b0", truncated.CustomId);
        Assert.AreEqual(FinishReason.Truncated, truncated.Response!.Finish);
        Assert.AreEqual(new LlmUsage(10, 5), truncated.Response.Usage);
        Assert.AreEqual(20, client.Usage.TotalTokens);
        Assert.AreEqual("b1", failed.CustomId);
        Assert.AreEqual("Bad Request: invalid schema", failed.Error);
        Assert.IsNull(foreign);
    }

    private InvocationContext Context() => new()
    {
        AuthenticationCredentialsProviders =
        [
            new AuthenticationCredentialsProvider(CredNames.ConnectionType, ConnectionTypes.GeminiApiKey),
            new AuthenticationCredentialsProvider(CredNames.ApiKey, "unused")
        ],
        Logger = new Logger("Debug") { LogWarning = (message, _) => _loggedWarnings.Add(message ?? string.Empty) }
    };

    private List<Segment> Segments(FileReference file)
    {
        var loaded = Transformation.Load(new MemoryStream(Encoding.UTF8.GetBytes(_files.Read(file))), file.Name);
        Assert.IsTrue(loaded.Success, loaded.Error);
        return loaded.Value.GetUnits().SelectMany(unit => unit.Segments).ToList();
    }

    // Starting a job may only call the synchronous client for language detection or a summary; these
    // tests avoid both, so any call is a bug.
    private sealed class SilentClient : ILlmClient
    {
        public LlmCapabilities Capabilities { get; } = new(SupportsStructuredOutput: true, ContextTokens: 1_048_576, MaxOutputTokens: 65_535);

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The synchronous client should not be called.");
    }

    private sealed class ScriptedTranslationActions(InvocationContext context, IFileManagementClient files, IBatchLlmClient batch)
        : TranslationActions(context, files)
    {
        protected override ILlmClient CreateLlmClient(string model, PromptRequest settings) => new SilentClient();

        protected override IBatchLlmClient CreateBatchClient(string? model, PromptRequest settings) => batch;
    }

    private sealed class ScriptedEditActions(InvocationContext context, IFileManagementClient files, IBatchLlmClient batch)
        : EditActions(context, files)
    {
        protected override ILlmClient CreateLlmClient(string model, PromptRequest settings) => new SilentClient();

        protected override IBatchLlmClient CreateBatchClient(string? model, PromptRequest settings) => batch;
    }

    private sealed class ScriptedBatchActions(InvocationContext context, IFileManagementClient files, IBatchLlmClient batch)
        : BatchActions(context, files)
    {
        protected override IBatchLlmClient CreateBatchClient(string? model, PromptRequest settings) => batch;
    }
}
