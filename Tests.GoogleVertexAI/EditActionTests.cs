using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using GoogleVertexAI.Base;
using Newtonsoft.Json;
using System.Globalization;

namespace Tests.GoogleVertexAI;

[TestClass]
public class EditActionTests : TestBase
{
    private const string ModelName = "gemini-2.5-flash";
    private const string ShortenModelName = "gemini-2.5-flash";

    [TestMethod]
    public async Task ShortenContent_Xliff_ShortensOverLimitTargets()
    {
        var actions = new EditActions(InvocationContext, FileManager);

        var results = await actions.ShortenContent(
            new ShortenContentRequest
            {
                Files = [new FileReference { Name = "shorten-content-test.xliff" }],
                AIModel = ShortenModelName,
                SegmentStates = [],
                BatchSize = 1,
                RetryCount = 3,
            },
            "Use German. For the over-limit target, a valid concise option is `Kurztitel`.",
            null,
            new PromptRequest { Temperature = 0.2f, MaxOutputTokens = 1000 });

        foreach (var result in results.Results)
        {
            var output = await ParseOutput(result.File);
            var shortenedUnit = output.GetUnits().Single(x => x.Id == "translated-over-limit");
            var reviewedUnit = output.GetUnits().Single(x => x.Id == "reviewed-over-limit");
            var underLimitUnit = output.GetUnits().Single(x => x.Id == "translated-under-limit");
            var noRestrictionUnit = output.GetUnits().Single(x => x.Id == "no-restriction");
            var shortenedTarget = shortenedUnit.Segments.Single().GetTarget();

            Assert.AreEqual(4, result.TotalUnitsCount);
            Assert.AreEqual(3, result.UnitsWithRestrictionCount);
            Assert.AreEqual(2, result.UnitsMatchedFilterCount);
            Assert.AreEqual(1, result.UnitsOverLimitCount);
            Assert.AreEqual(1, result.UnitsUpdatedCount);
            Assert.AreEqual(0, result.UnitsRemainingOverLimitCount);
            Assert.IsTrue(result.ProcessedBatchesCount >= 1);
            Assert.IsTrue(result.Usage.TotalTokens > 0);
            Assert.IsTrue(CountGraphemes(shortenedTarget) <= 10, $"Target `{shortenedTarget}` exceeds 10 graphemes.");
            Assert.AreEqual(SegmentState.Translated, shortenedUnit.Segments.Single().State);
            Assert.AreEqual("This reviewed target is long", reviewedUnit.Segments.Single().GetTarget());
            Assert.AreEqual(SegmentState.Reviewed, reviewedUnit.Segments.Single().State);
            Assert.AreEqual("Short target", underLimitUnit.Segments.Single().GetTarget());
            Assert.AreEqual("This target has no restriction", noRestrictionUnit.Segments.Single().GetTarget());
            Assert.IsTrue(result.PromptTemplate.Contains("{sourceLanguage}"));
            Assert.IsFalse(result.ErrorMessages?.Any() ?? false, string.Join(Environment.NewLine, result.ErrorMessages ?? [])); 
        }
    }

    [TestMethod]
    public async Task ShortenContent_CharacterLimitSample_ShortensOverLimitTargets()
    {
        var actions = new EditActions(InvocationContext, FileManager);

        var results = await actions.ShortenContent(
            new ShortenContentRequest
            {
                Files = [new FileReference { Name = "Home page with character limits_en-US-en_us-de-T.xliff" }],
                AIModel = ShortenModelName,
                BatchSize = 1,
                RetryCount = 3,
            },
            "Use German. For the long Multilingual Content Operations unit, return exactly this target segment: `Mehrsprachige Content Ops machen Sprache zum Wachstumsmotor. Blackbird hilft Inhalten, Ziele zu erreichen und Verbindungen zu schaffen.`",
            null,
            new PromptRequest { Temperature = 0.0f, MaxOutputTokens = 1000 });

        foreach (var result in results.Results)
        {
            var output = await ParseOutput(result.File);
            var translatedRestrictedUnit = output.GetUnits().Single(x => x.Id == "vQl02fa2sN2aX16G1_dc10:1");
            var initialRestrictedUnit = output.GetUnits().Single(x => x.Id == "vQl02fa2sN2aX16G1_dc10:0");
            var shortenedTarget = translatedRestrictedUnit.Segments.Single().GetTarget();

            Assert.AreEqual(6, result.TotalUnitsCount);
            Assert.AreEqual(2, result.UnitsWithRestrictionCount);
            Assert.AreEqual(2, result.UnitsMatchedFilterCount);
            Assert.AreEqual(1, result.UnitsOverLimitCount);
            Assert.AreEqual(1, result.UnitsUpdatedCount);
            Assert.AreEqual(0, result.UnitsRemainingOverLimitCount);
            Assert.IsTrue(result.ProcessedBatchesCount >= 1);
            Assert.IsTrue(result.Usage.TotalTokens > 0);
            Assert.IsTrue(CountGraphemes(shortenedTarget) <= 150, $"Target `{shortenedTarget}` exceeds 150 graphemes.");
            Assert.AreEqual(SegmentState.Translated, translatedRestrictedUnit.Segments.Single().State);
            Assert.AreEqual(SegmentState.Initial, initialRestrictedUnit.Segments.Single().State);
            Assert.IsFalse(result.ErrorMessages?.Any() ?? false, string.Join(Environment.NewLine, result.ErrorMessages ?? []));
        }
    }

    [TestMethod]
    public async Task Edit_xliff()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var translateRequest = new EditFileRequest
        {
            File = new FileReference { Name = "contentful.html.xlf" },
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var result = await actions.EditContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        

        PrintResult(result);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.File.Name.Contains("contentful"));
        Assert.IsEmpty(result.ErrorMessages!, "Errors received while editing content");
    }

    [TestMethod]
    public async Task EditText_WithSerbianLocale_ReturnsLocalizedText()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var localizeRequest = new EditTextRequest
        {
            SourceText = "Develop and implement an HR strategy that drives organizational productivity and supports company's business goals. Design and oversee processes that promote team efficiency and operational effectiveness while reducing complexity and redundancies.",
            TargetText = "Razviti i primeniti HR strategiju koja podstiče organizacionu produktivnost i podržava poslovne ciljeve kompanije. Dizajnirati i nadgledati procese koji promovišu efikasnost tima i operativnu efektivnost, uz smanjenje kompleksnosti i redundanci.",
            TargetLanguage = "sr-Latn-RS",
            AIModel = ModelName,
        };

        var glossaryRequest = new GlossaryRequest();
        string? systemMessage = null;
        var result = await actions.EditText(localizeRequest, new PromptRequest { }, systemMessage, glossaryRequest);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.EditedText);
        Console.WriteLine("Original: " + localizeRequest.TargetText);
        Console.WriteLine("Localized: " + result.EditedText);
    }

    [TestMethod]
    public async Task Batch_edit_text_returns_valid_xliff()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var file = new FileReference { Name = "contentful.html.xlf" };
        var translateRequest = new EditFileRequest
        {
            File = file,
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var startbatchResopnse = await actions.BatchEditContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest, new());
        Assert.IsNotNull(startbatchResopnse);
        Console.WriteLine(startbatchResopnse.JobName);

        var polling = new BatchPolling(InvocationContext);


        var result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>() {
            Memory = new BatchMemory
            {
                LastPollingTime = DateTime.UtcNow,
                Triggered = false
            }
        }, new BatchIdentifier { JobName = startbatchResopnse.JobName });

        while (!result.FlyBird)
        {
            await Task.Delay(3000);
            result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>()
            {
                Memory = new BatchMemory
                {
                    LastPollingTime = DateTime.UtcNow,
                    Triggered = false
                }
            }, new BatchIdentifier { JobName = startbatchResopnse.JobName });
        }

        var batchActions = new BatchActions(InvocationContext, FileManager);

        var finalResult = await batchActions.DownloadXliffFromBatch(startbatchResopnse.JobName, new GetBatchResultRequest { OriginalXliff = startbatchResopnse.TransformationFile });

        Console.WriteLine(JsonConvert.SerializeObject(finalResult, Formatting.Indented));

        Assert.IsNotNull(finalResult.File);
    }

    private static async Task<Transformation> ParseOutput(FileReference file)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var projectDirectory = Directory.GetParent(baseDirectory)!.Parent!.Parent!.Parent!.FullName;
        var path = Path.Combine(projectDirectory, "TestFiles", "Output", file.Name);
        await using var stream = File.OpenRead(path);
        
        var loadResult = Transformation.Load(stream, file.Name);
        return loadResult.Success ? loadResult.Value : throw new Exception(loadResult.Error);
    }

    private static int CountGraphemes(string? value)
        => StringInfo.ParseCombiningCharacters(value ?? string.Empty).Length;
}
