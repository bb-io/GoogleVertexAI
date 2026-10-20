using System.Globalization;
using System.Text;
using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using Part = Google.Cloud.AIPlatform.V1.Part;
using Newtonsoft.Json.Linq;

namespace Tests.GoogleVertexAI.Ai;

// Offline, no API spend: the provider is replaced through CreateLlmClient in action subclasses.
[TestClass]
public class OfflineActionTests
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

    // Unusable answers end as warnings in the log and in the existing "Error messages" output; the file
    // is returned with the segments untouched instead of the action failing.
    [TestMethod]
    public async Task TranslateContent_UnusableAnswers_LogsWarningsAndLeavesSegmentsUntouched()
    {
        var file = _files.Add("broken.xlf", TwoInitialSegments);

        var result = await new ScriptedTranslationActions(Context(), _files, ScriptedLlmClient.Unusable()).TranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "de", AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        Assert.AreEqual(0, result.TargetsUpdatedCount);
        Assert.AreEqual(2, result.TotalTranslatable);
        Assert.IsNotEmpty(_loggedWarnings);
        Assert.IsTrue(_loggedWarnings.All(warning => warning.Contains("[GoogleGemini]")), string.Join(Environment.NewLine, _loggedWarnings));
        Assert.IsNotEmpty(result.ErrorMessages!);
        Assert.IsTrue(Segments(result.File).All(segment => string.IsNullOrEmpty(segment.GetTarget())));
    }

    [TestMethod]
    public async Task TranslateText_UnusableAnswer_Throws()
    {
        var actions = new ScriptedTranslationActions(Context(), _files, ScriptedLlmClient.Unusable());

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() => actions.LocalizeText(
            new TranslateTextRequest { Text = "Hello", SourceLanguage = "en", TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest()));

        Assert.IsNotEmpty(_loggedWarnings);
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }

    [TestMethod]
    public async Task EditText_UnusableAnswer_Throws()
    {
        var actions = new ScriptedEditActions(Context(), _files, ScriptedLlmClient.Unusable());

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() => actions.EditText(
            new EditTextRequest { SourceText = "Good morning.", TargetText = "Guten Morgen.", SourceLanguage = "en", TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest()));
    }

    // Edit sends only Translated segments and marks what it processed as Reviewed.
    [TestMethod]
    public async Task EditContent_ScriptedAnswers_UpdatesTargetsAndMarksReviewed()
    {
        var file = _files.Add("edit.xlf", TwoTranslatedSegments);

        var result = await new ScriptedEditActions(Context(), _files, ScriptedLlmClient.Texts("Hallo.")).EditContent(
            new EditFileRequest { File = file, AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        var segments = Segments(result.File);
        Assert.AreEqual(2, result.TotalSegmentsReviewed);
        Assert.AreEqual(2, result.TotalSegmentsUpdated);
        Assert.IsEmpty(result.ErrorMessages!);
        Assert.IsTrue(segments.All(segment => segment.GetTarget() == "Hallo." && segment.State == SegmentState.Reviewed));
    }

    // The 0-100 threshold and average survive the library's 0-1 scores, and segments at or above the
    // threshold get the new state.
    [TestMethod]
    public async Task Score_ScriptedScores_KeepsZeroToHundredScale()
    {
        var file = _files.Add("score.xlf", TwoTranslatedSegments);

        var result = await new ScriptedReviewActions(Context(), _files, ScriptedLlmClient.Scores(0.9)).Score(
            new AIModelRequest { AIModel = Model },
            new ScoreRequest { File = file, Threshold = 80, NewState = "final" },
            null,
            new PromptRequest());

        Assert.AreEqual(2, result.TotalUnitsProcessed);
        Assert.AreEqual(90, result.AverageScore, 0.001);
        Assert.AreEqual(2, result.TotalSegmentsFinalized);
        Assert.AreEqual(0, result.TotalUnitsUnderThreshhold);
        Assert.AreEqual(0, result.PercentageUnitsUnderThreshold);
        Assert.IsTrue(Segments(result.File).All(segment => segment.State == SegmentState.Final));
    }

    // Without a threshold the default of 80 applies: 85 is promoted to the default new state (Reviewed).
    [TestMethod]
    public async Task Score_WithoutThreshold_UsesDefaultOfEighty()
    {
        var file = _files.Add("score.xlf", TwoTranslatedSegments);

        var result = await new ScriptedReviewActions(Context(), _files, ScriptedLlmClient.Scores(0.85)).Score(
            new AIModelRequest { AIModel = Model },
            new ScoreRequest { File = file },
            null,
            new PromptRequest());

        Assert.AreEqual(85, result.AverageScore, 0.001);
        Assert.AreEqual(2, result.TotalSegmentsFinalized);
        Assert.AreEqual(0, result.TotalUnitsUnderThreshhold);
        Assert.IsTrue(Segments(result.File).All(segment => segment.State == SegmentState.Reviewed));
    }

    // The client's threshold is what counts: with 90, scores of 85 are neither promoted nor above it.
    [TestMethod]
    public async Task Score_ClientThreshold_DecidesPromotionAndUnderThreshold()
    {
        var file = _files.Add("score.xlf", TwoTranslatedSegments);

        var result = await new ScriptedReviewActions(Context(), _files, ScriptedLlmClient.Scores(0.85)).Score(
            new AIModelRequest { AIModel = Model },
            new ScoreRequest { File = file, Threshold = 90 },
            null,
            new PromptRequest());

        var scoreThresholds = LoadXliff(_files.Read(result.File), result.File.Name).GetUnits()
            .Select(unit => unit.Quality.ScoreThreshold)
            .ToList();
        Assert.AreEqual(0, result.TotalSegmentsFinalized);
        Assert.AreEqual(2, result.TotalUnitsUnderThreshhold);
        Assert.AreEqual(100, result.PercentageUnitsUnderThreshold, 0.001);
        Assert.IsTrue(scoreThresholds.All(value => value is 0.9), string.Join(", ", scoreThresholds));
        Assert.IsTrue(Segments(result.File).All(segment => segment.State == SegmentState.Translated));
    }

    [TestMethod]
    public async Task Score_ThresholdOutOfRange_ThrowsBeforeAnyCall()
    {
        var file = _files.Add("score.xlf", TwoTranslatedSegments);

        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() =>
            new ScriptedReviewActions(Context(), _files, ScriptedLlmClient.Never()).Score(
                new AIModelRequest { AIModel = Model },
                new ScoreRequest { File = file, Threshold = 150 },
                null,
                new PromptRequest()));
    }

    // The "System prompt" input is put in front of the library prompt; the response contract stays.
    [TestMethod]
    public async Task ShortenContent_SystemPrompt_IsPrependedAndKeepsResponseFormat()
    {
        const string systemPrompt = "You write for a crypto exchange.";
        var file = _files.Add("guard-check.xliff", Fixture("guard-check.xliff"));
        // The model sees text placeholders XML-escaped and answers the same way.
        var client = ScriptedLlmClient.Texts("{1&gt;5 USDT&lt;1}");

        var response = await new ScriptedEditActions(Context(), _files, client).ShortenContent(
            new ShortenContentRequest { Files = [file], AIModel = Model, RetryCount = 0 },
            null,
            systemPrompt,
            new PromptRequest());

        var result = response.Results.Single();
        Assert.IsEmpty(result.ErrorMessages!, string.Join(Environment.NewLine, result.ErrorMessages!));
        Assert.AreEqual(1, result.UnitsUpdatedCount);
        Assert.AreEqual("{1&gt;5 USDT&lt;1}", Segments(result.File).Single().GetTarget());
        Assert.IsNotEmpty(client.Requests);
        Assert.IsTrue(client.Requests.All(request =>
            request.SystemPrompt.StartsWith(systemPrompt) && request.SystemPrompt.Contains("### OUTPUT FORMAT (STRICT)")));
    }

    // Ported from ShortenHelperTests: an answer that breaks a {1>...<1} placeholder is never written.
    [TestMethod]
    public async Task ShortenContent_WhenGeminiBreaksTag_KeepsOriginal()
    {
        var file = _files.Add("guard-check.xliff", Fixture("guard-check.xliff"));
        var before = LoadXliff(Fixture("guard-check.xliff"), file.Name).GetUnits().Single().Segments.Single().GetTarget();

        var response = await new ScriptedEditActions(Context(), _files, ScriptedLlmClient.Texts("{1&gt;5 USDT&lt;1&gt;")).ShortenContent(
            new ShortenContentRequest { Files = [file], AIModel = Model, RetryCount = 0 },
            null,
            null,
            new PromptRequest());

        var result = response.Results.Single();
        Assert.AreEqual(0, result.UnitsUpdatedCount);
        Assert.AreEqual(before, Segments(result.File).Single().GetTarget());
        Assert.Contains(message => message.Contains("changed the tags"), result.ErrorMessages!);
    }

    // Ported from ShortenHelperTests: {1>...<1} placeholders do not count toward the size restriction,
    // so a title that fits without them is not sent to the model.
    [TestMethod]
    public async Task ShortenContent_TagsNotCounted_NotFlagged()
    {
        var file = _files.Add("char-count-check.xliff", Fixture("char-count-check.xliff"));

        var response = await new ScriptedEditActions(Context(), _files, ScriptedLlmClient.Never()).ShortenContent(
            new ShortenContentRequest { Files = [file], AIModel = Model },
            null,
            null,
            new PromptRequest());

        var result = response.Results.Single();
        Assert.AreEqual(1, result.UnitsWithRestrictionCount, string.Join(Environment.NewLine, result.ErrorMessages ?? []));
        Assert.AreEqual(0, result.UnitsOverLimitCount);
        Assert.AreEqual(0, result.UnitsUpdatedCount);
    }

    // The client maps Gemini's finish reasons, keeps Gemini's own usage, and sends the library schema as
    // a standard JSON Schema.
    [TestMethod]
    public async Task GeminiLlmClient_MapsFinishReasonUsageAndSchema()
    {
        var generative = new FakeGenerativeModelClient();
        var client = new GeminiLlmClient(generative, Model, new PromptRequest { MaxOutputTokens = 1000 });

        generative.Next = new GeneratedTextDto("{}", new UsageDto(100, 20, 150), "MAX_TOKENS");
        var truncated = await client.CompleteAsync(
            new LlmRequest("system", "user", new JsonSchemaSpec("Test", """{ "type": "object" }""")),
            CancellationToken.None);

        generative.Next = new GeneratedTextDto("{}", new UsageDto(10, 2, 12), "STOP");
        var stopped = await client.CompleteAsync(new LlmRequest("system", "user"), CancellationToken.None);

        Assert.AreEqual(FinishReason.Truncated, truncated.Finish);
        Assert.AreEqual(FinishReason.Stop, stopped.Finish);
        Assert.AreEqual(FinishReason.Truncated, GeminiLlmClient.MapFinishReason("MaxTokens"));
        Assert.AreEqual(FinishReason.ContentFilter, GeminiLlmClient.MapFinishReason("SAFETY"));
        Assert.AreEqual(FinishReason.Stop, GeminiLlmClient.MapFinishReason(null));
        Assert.AreEqual(new LlmUsage(100, 20), truncated.Usage);
        Assert.AreEqual(162, client.Usage.TotalTokens);
        Assert.AreEqual(1000, client.Capabilities.MaxOutputTokens);
        Assert.IsTrue(client.Capabilities.SupportsStructuredOutput);
        Assert.IsInstanceOfType<JObject>(generative.JsonSchemas[0]);
        Assert.IsNull(generative.JsonSchemas[1]);
    }

    [TestMethod]
    public void GeminiLlmClient_Capabilities_FollowModelLimits()
    {
        Assert.AreEqual(65535, GeminiLlmClient.CapabilitiesFor("gemini-2.5-flash", null).MaxOutputTokens);
        Assert.AreEqual(1_048_576, GeminiLlmClient.CapabilitiesFor("gemini-2.5-flash", null).ContextTokens);
        Assert.AreEqual(8191, GeminiLlmClient.CapabilitiesFor("gemini-2.0-flash-001", null).MaxOutputTokens);
        Assert.AreEqual(4096, GeminiLlmClient.CapabilitiesFor("1234567890", null).MaxOutputTokens);
        Assert.AreEqual(128_000, GeminiLlmClient.CapabilitiesFor("1234567890", null).ContextTokens);
        Assert.AreEqual(4096, GeminiLlmClient.CapabilitiesFor("1234567890", 100_000).MaxOutputTokens);
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

    private List<Segment> Segments(FileReference file) =>
        LoadXliff(_files.Read(file), file.Name).GetUnits().SelectMany(unit => unit.Segments).ToList();

    private static Transformation LoadXliff(string content, string name)
    {
        var loaded = Transformation.Load(new MemoryStream(Encoding.UTF8.GetBytes(content)), name);
        Assert.IsTrue(loaded.Success, loaded.Error);
        return loaded.Value;
    }

    private static string Fixture(string name)
    {
        var projectDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
        return File.ReadAllText(Path.Combine(projectDirectory, "TestFiles", "Input", name), Encoding.UTF8);
    }

    private sealed class ScriptedTranslationActions(InvocationContext context, IFileManagementClient files, ILlmClient client)
        : TranslationActions(context, files)
    {
        protected override ILlmClient CreateLlmClient(string model, PromptRequest settings) => client;
    }

    private sealed class ScriptedEditActions(InvocationContext context, IFileManagementClient files, ILlmClient client)
        : EditActions(context, files)
    {
        protected override ILlmClient CreateLlmClient(string model, PromptRequest settings) => client;
    }

    private sealed class ScriptedReviewActions(InvocationContext context, IFileManagementClient files, ILlmClient client)
        : ReviewActions(context, files)
    {
        protected override ILlmClient CreateLlmClient(string model, PromptRequest settings) => client;
    }

    private sealed class FakeGenerativeModelClient : IGenerativeModelClient
    {
        public GeneratedTextDto Next { get; set; } = new(string.Empty, new UsageDto(), null);

        public List<object?> JsonSchemas { get; } = [];

        public Task ValidateConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(string Result, UsageDto Usage)> GenerateTextAsync(
            PromptRequest input, string modelId, string prompt, string? systemPrompt = null, object? schema = null,
            IEnumerable<Part>? files = null, CancellationToken cancellationToken = default) =>
            Task.FromResult((Next.Text, Next.Usage));

        public Task<GeneratedTextDto> GenerateAsync(
            PromptRequest input, string modelId, string prompt, string? systemPrompt = null, object? schema = null,
            object? responseJsonSchema = null, IEnumerable<Part>? files = null, CancellationToken cancellationToken = default)
        {
            JsonSchemas.Add(responseJsonSchema);
            return Task.FromResult(Next);
        }
    }
}
