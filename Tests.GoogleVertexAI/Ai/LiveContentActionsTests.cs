using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI.Ai;

// Live: every test calls the real model through the connection configured in appsettings.json, on the
// repository fixtures. Fixtures are read from disk and modified only in memory.
[TestClass]
public class LiveContentActionsTests : TestBase
{
    private const string Model = "gemini-2.5-flash";

    private readonly InMemoryFileManagementClient _files = new();
    private readonly List<string> _loggedWarnings = [];

    // A plain HTML page has no source language, so the action detects it, translates every segment and
    // returns the page in its original format. 24 units also exercise the document summary.
    [TestMethod]
    public async Task TranslateContent_ContentfulHtml_ReturnsTranslatedHtml()
    {
        var file = _files.Add("contentful.html", await ReadInputAsync("contentful.html"), "text/html");

        var result = await new TranslationActions(WithWarningLog(), _files).TranslateContent(
            new TranslateFileRequest { File = file, TargetLanguage = "nl", AIModel = Model, OutputFileHandling = "original" },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        PrintResult(result);
        var html = _files.Read(result.File);
        AssertNoWarningsLogged();
        Assert.IsEmpty(result.ErrorMessages!);
        Assert.IsGreaterThan(0, result.TotalTranslatable);
        Assert.AreEqual(result.TotalTranslatable, result.TargetsUpdatedCount);
        Assert.Contains("<h1", html);
        Assert.DoesNotContain("Exploring Loire Valley Wines", html);
    }

    // A planted mistranslation (Rhine for Loire) must be corrected, and edited segments marked Reviewed.
    [TestMethod]
    public async Task EditContent_PlantedMistranslation_IsCorrected()
    {
        var xliff = PlantTarget(await ReadInputAsync("contentful.html.xlf"), "The Loire Valley", "Het Rijndal");
        var file = _files.Add("contentful.html.xlf", xliff);

        var result = await new EditActions(WithWarningLog(), _files).EditContent(
            new EditFileRequest { File = file, AIModel = Model, GenerateDocumentSummary = false },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        PrintResult(result);
        var segments = Segments(result.File);
        var planted = segments.Where(segment => segment.GetSource() == "The Loire Valley").ToList();
        AssertNoWarningsLogged();
        Assert.IsEmpty(result.ErrorMessages!);
        Assert.AreEqual(20, result.TotalSegmentsReviewed);
        Assert.IsNotEmpty(planted);
        Assert.IsTrue(planted.All(segment => segment.GetTarget().Contains("Loire")), string.Join(" | ", planted.Select(segment => segment.GetTarget())));
        Assert.AreEqual(20, segments.Count(segment => segment.State == SegmentState.Reviewed));
        Assert.AreEqual(0, segments.Count(segment => segment.State == SegmentState.Translated));
    }

    [TestMethod]
    public async Task TranslateText_RealModel_ReturnsTranslation()
    {
        const string source = "Please restart the application.";

        var result = await new TranslationActions(WithWarningLog(), _files).LocalizeText(
            new TranslateTextRequest { Text = source, SourceLanguage = "en", TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        PrintResult(result);
        AssertNoWarningsLogged();
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TranslatedText));
        Assert.AreNotEqual(source, result.TranslatedText);
    }

    // Edit text must fix a planted mistranslation (dog for cat) and leave a correct target alone.
    [TestMethod]
    public async Task EditText_RealModel_CorrectsMistranslationAndKeepsCorrectTarget()
    {
        var actions = new EditActions(WithWarningLog(), _files);

        var wrong = await actions.EditText(
            new EditTextRequest { SourceText = "The cat is sleeping on the sofa.", TargetText = "Der Hund schläft auf dem Sofa.", SourceLanguage = "en", TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest());
        var correct = await actions.EditText(
            new EditTextRequest { SourceText = "Good morning.", TargetText = "Guten Morgen.", SourceLanguage = "en", TargetLanguage = "de", AIModel = Model },
            new PromptRequest(),
            null,
            new GlossaryRequest());

        PrintResult(wrong);
        PrintResult(correct);
        AssertNoWarningsLogged();
        Assert.Contains("Katze", wrong.EditedText);
        Assert.AreEqual("Guten Morgen.", correct.EditedText);
    }

    // A target over its slr:sizeRestriction is shortened until it fits; the state is not changed.
    [TestMethod]
    public async Task ShortenContent_OverLimitTarget_FitsRestriction()
    {
        var xliff = Regex.Replace(
            await ReadInputAsync("char-count-check.xliff"),
            "<source>.*?</source>\\s*<target>.*?</target>",
            "<source>The champion's great challenge for every player</source>\n        <target>Die große Herausforderung des Champions für jeden Spieler</target>");
        var file = _files.Add("char-count-check.xliff", xliff);

        var response = await new EditActions(WithWarningLog(), _files).ShortenContent(
            new ShortenContentRequest { Files = [file], AIModel = Model },
            "Use German.",
            null,
            new PromptRequest());

        var result = response.Results.Single();
        PrintResult(result);
        var segment = Segments(result.File).Single();
        Assert.IsEmpty(result.ErrorMessages!, string.Join(Environment.NewLine, result.ErrorMessages!));
        AssertNoWarningsLogged();
        Assert.AreEqual(1, result.UnitsOverLimitCount);
        Assert.AreEqual(1, result.UnitsUpdatedCount);
        Assert.IsGreaterThan(0, result.Usage.TotalTokens);
        Assert.IsLessThanOrEqualTo(22, new StringInfo(segment.GetTarget()).LengthInTextElements, segment.GetTarget());
        Assert.AreEqual(SegmentState.Translated, segment.State);
    }

    // Quality estimation must score a planted nonsense target below the file average, keep the 0-100
    // scale, and promote segments at or above the threshold.
    [TestMethod]
    public async Task Score_PlantedNonsense_ScoresBelowAverage()
    {
        var xliff = PlantTarget(await ReadInputAsync("contentful.html.xlf"), "The Loire Valley", "De maan is van groene kaas");
        var file = _files.Add("contentful.html.xlf", xliff);

        var result = await new ReviewActions(WithWarningLog(), _files).Score(
            new AIModelRequest { AIModel = Model },
            new ScoreRequest { File = file, Threshold = 80 },
            null,
            new PromptRequest());

        PrintResult(result);
        var segments = Segments(result.File);
        var plantedScores = segments
            .Where(segment => segment.GetSource() == "The Loire Valley")
            .Select(segment => double.Parse(segment.TargetAttributes.Single(attribute => attribute.Name.LocalName == "extradata").Value, CultureInfo.InvariantCulture) * 100)
            .ToList();
        AssertNoWarningsLogged();
        Assert.AreEqual(20, result.TotalUnitsProcessed);
        Assert.IsTrue(result.AverageScore is > 0 and <= 100, $"average {result.AverageScore}");
        Assert.IsNotEmpty(plantedScores);
        Assert.IsTrue(plantedScores.All(score => score < result.AverageScore && score < 80), $"planted {string.Join(", ", plantedScores)} vs average {result.AverageScore}");
        Assert.IsGreaterThanOrEqualTo(1, result.TotalUnitsUnderThreshhold);
        Assert.IsGreaterThan(0, result.Usage.TotalTokens);
        Assert.AreEqual(result.TotalSegmentsFinalized, segments.Count(segment => segment.State == SegmentState.Reviewed));
    }

    // Warnings are not part of the action output; the library logs them through InvocationContext.Logger.
    private InvocationContext WithWarningLog()
    {
        InvocationContext.Logger = new Logger("Debug") { LogWarning = (message, _) => _loggedWarnings.Add(message ?? string.Empty) };
        return InvocationContext;
    }

    private void AssertNoWarningsLogged() => Assert.IsEmpty(_loggedWarnings, string.Join(Environment.NewLine, _loggedWarnings));

    private async Task<string> ReadInputAsync(string name)
    {
        await using var stream = await FileManager.DownloadAsync(new FileReference { Name = name });
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // The regex skips the escaped copy of the text in <skeleton>; a sentence can occur in several segments.
    private static string PlantTarget(string xliff, string source, string plantedTarget)
    {
        var pattern = new Regex($@"(<source>{Regex.Escape(source)}</source>\s*<target>)[^<]*(</target>)");
        Assert.IsGreaterThan(0, pattern.Matches(xliff).Count, $"No segment with source '{source}'.");
        return pattern.Replace(xliff, $"${{1}}{plantedTarget}${{2}}");
    }

    private List<Segment> Segments(FileReference file)
    {
        var loaded = Transformation.Load(new MemoryStream(Encoding.UTF8.GetBytes(_files.Read(file))), file.Name);
        Assert.IsTrue(loaded.Success, loaded.Error);
        return loaded.Value.GetUnits().SelectMany(unit => unit.Segments).ToList();
    }
}
