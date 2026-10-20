using System.Text;
using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI.Ai;

// Live: submits a real Vertex batch prediction job and waits for it, which can take from minutes to hours.
// Skipped unless RUN_LIVE_BATCH=1 and the configured connection is a service account, so a normal test
// run never waits on a batch.
[TestClass]
public class LiveBackgroundActionsTests : TestBase
{
    private const string RunVariable = "RUN_LIVE_BATCH";
    private const string Model = "gemini-2.5-flash";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(90);

    private const string ThreeSegments = """
        <?xml version="1.0" encoding="utf-8"?>
        <xliff xmlns="urn:oasis:names:tc:xliff:document:2.0" version="2.0" srcLang="en" trgLang="de">
          <file id="f1">
            <unit id="u1"><segment id="s1"><source>The weather is lovely today.</source></segment></unit>
            <unit id="u2"><segment id="s2"><source>Please restart the application.</source></segment></unit>
            <unit id="u3"><segment id="s3"><source>Our office opens at nine.</source></segment></unit>
          </file>
        </xliff>
        """;

    private readonly InMemoryFileManagementClient _files = new();

    // Runs the way a Bird does: start, poll the event until it fires, poll once more, download.
    [TestMethod]
    public async Task TranslateInBackground_RealBatch_EventFiresOnceAndDownloadTranslates()
    {
        if (Environment.GetEnvironmentVariable(RunVariable) != "1")
            Assert.Inconclusive($"Set {RunVariable}=1 to submit a real Vertex batch job.");

        if (InvocationContext.AuthenticationCredentialsProviders.FirstOrDefault(x => x.KeyName == CredNames.ConnectionType)?.Value != ConnectionTypes.ServiceAccount)
            Assert.Inconclusive("Vertex batch prediction needs a service account connection in appsettings.json.");

        var file = _files.Add("live-background.xlf", ThreeSegments);

        var started = await new TranslationActions(InvocationContext, _files).BatchTranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest(),
            new BucketRequest());

        PrintResult(started);
        Assert.IsFalse(string.IsNullOrWhiteSpace(started.JobName));
        Assert.AreEqual(3, started.SegmentsToProcessCount);

        var polling = new BatchPolling(InvocationContext);
        var identifier = new BatchIdentifier { JobName = started.JobName };
        var poll = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>(), identifier);
        var deadline = DateTime.UtcNow + MaxWait;

        do
        {
            await Task.Delay(PollInterval);
            poll = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory> { Memory = poll.Memory }, identifier);
        }
        while (!poll.FlyBird && DateTime.UtcNow < deadline);

        Assert.IsTrue(poll.FlyBird, $"The batch did not finish within {MaxWait}.");
        PrintResult(poll.Result!);

        var again = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory> { Memory = poll.Memory }, identifier);
        Assert.IsFalse(again.FlyBird, "The event fired twice for the same job.");

        var downloaded = await new BatchActions(InvocationContext, _files).DownloadXliffFromBatch(
            null,
            new GetBatchResultRequest { OriginalXliff = started.TransformationFile });

        PrintResult(downloaded);
        var segments = Segments(downloaded.File);
        Assert.AreEqual(3, downloaded.UpdatedSegmentsCount);
        Assert.AreEqual(0, downloaded.UnresolvedSegmentsCount, string.Join(Environment.NewLine, downloaded.Warnings));
        Assert.IsGreaterThan(0, downloaded.Usage.TotalTokens);
        Assert.IsTrue(segments.All(segment =>
            segment.State == SegmentState.Translated
            && !string.IsNullOrWhiteSpace(segment.GetTarget())
            && segment.GetTarget() != segment.GetSource()));
    }

    private List<Segment> Segments(FileReference file)
    {
        var loaded = Transformation.Load(new MemoryStream(Encoding.UTF8.GetBytes(_files.Read(file))), file.Name);
        Assert.IsTrue(loaded.Success, loaded.Error);
        return loaded.Value.GetUnits().SelectMany(unit => unit.Segments).ToList();
    }
}
