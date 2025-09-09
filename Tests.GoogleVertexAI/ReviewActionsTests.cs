using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Filters.Enums;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class ReviewActionsTests : TestBase
{
    private ReviewActions _actions => new(InvocationContext, FileManager);

    private const string ModelName = "gemini-2.5-flash-lite";
    private const string TestFileName = "contentful.html.xlf";

    [TestMethod]
    public async Task Score_WithoutThreshold_ReturnsAverageScore()
    {
        // Arrange
        var model = new AIModelRequest { AIModel = ModelName };
        var scoreRequest = new ScoreRequest
        {
            File = new FileReference { Name = TestFileName }
        };
        string? prompt = null;
        var promptRequest = new PromptRequest
        {
            MaxOutputTokens = 2500
        };

        // Act
        var result = await _actions.Score(model, scoreRequest, prompt, promptRequest);

        // Assert
        PrintResult(result);

        Assert.IsNotNull(result.File);
        Assert.IsTrue(result.AverageScore > 0);
        Assert.IsTrue(result.Usage.TotalTokens > 0);
    }

    [TestMethod]
    public async Task Score_WithThreshhold_SavesScoresAndChangesStates()
    {
        // Arrange
        var model = new AIModelRequest { AIModel = ModelName };
        var scoreRequest = new ScoreRequest
        {
            File = new FileReference { Name = TestFileName },
            Threshold = 99.0,
        };
        string? prompt = null;
        var promptRequest = new PromptRequest
        {
            MaxOutputTokens = 2500
        };

        // Act
        var result = await _actions.Score(model, scoreRequest, prompt, promptRequest);

        // Assert
        PrintResult(result);

        Assert.IsNotNull(result.File);
        Assert.IsTrue(result.AverageScore > 0);
        Assert.IsTrue(result.Usage.TotalTokens > 0);
    }

    [TestMethod]
    public async Task Score_WithoutTranslations_ReturnsWithZeroes()
    {
        // Arrange
        var model = new AIModelRequest { AIModel = ModelName };
        var scoreRequest = new ScoreRequest
        {
            File = new FileReference { Name = "contentful.html" },
            SourceLanguage = "en",
            TargetLanguage = "fr",
            Threshold = 99.0f,
            NewState = SegmentStateHelper.Serialize(SegmentState.Reviewed),
            SaveScores = true,
        };
        string? prompt = null;
        var promptRequest = new PromptRequest
        {
            MaxOutputTokens = 2500
        };

        // Act
        var result = await _actions.Score(model, scoreRequest, prompt, promptRequest);

        // Assert
        PrintResult(result);

        Assert.IsNotNull(result.File);
        Assert.AreEqual(0, result.AverageScore);
        Assert.AreEqual(0, result.Usage.TotalTokens);
    }
}