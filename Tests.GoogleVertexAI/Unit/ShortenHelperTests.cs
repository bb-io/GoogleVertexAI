using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;

namespace Tests.GoogleVertexAI.Unit;

[TestClass]
public class ShortenHelperTests
{
    [TestMethod]
    public async Task ShortenSingleFile_WhenGeminiBreaksTag_KeepsOriginal()
    {
        // Arrange
        var content = LoadInput("guard-check.xliff");
        var before = content.GetUnits().Single().Segments.Single().GetTarget();
        var promptRequest = new PromptRequest();

        var settings = new ShortenSettings(
            AiModel: "test", 
            BatchSize: 1500,
            RetryCount: 0,
            SystemPrompt: "sys",
            StatesToProcess: new HashSet<SegmentState> { SegmentState.Translated },
            AdditionalInstructions: null, 
            PromptTemplate: "tmpl");

        ShortenHelper.ExecutePrompt brokenAi = (_, _, _, _, _) => 
            Task.FromResult(("[{\"id\":0,\"targets\":[\"{1>5 USDT<1>\"]}]", new UsageDto()));

        // Act
        var (result, transformation) = await ShortenHelper.ShortenSingleFile(content, settings, promptRequest, brokenAi);

        // Assert
        Assert.AreEqual(0, result.UnitsUpdatedCount);
        Assert.AreEqual(before, transformation.GetUnits().Single().Segments.Single().GetTarget());
        Assert.Contains(m => m.Contains("changed the tags"), result.ErrorMessages!);
    }
    
    [TestMethod]
    public async Task ShortenSingleFile_TagsNotCounted_NotFlagged()
    {
        // Arrange
        var content = LoadInput("char-count-check.xliff");
        var promptRequest = new PromptRequest();

        var settings = new ShortenSettings(
            AiModel: "test",
            BatchSize: 1500, 
            RetryCount: 3,
            SystemPrompt: "sys",
            StatesToProcess: new HashSet<SegmentState> { SegmentState.Translated },
            AdditionalInstructions: null, 
            PromptTemplate: "tmpl");

        ShortenHelper.ExecutePrompt aiMustNotRun = (_, _, _, _, _) =>
            throw new Exception("Gemini should not be called - the title already fits");

        // Act
        var (result, _) = await ShortenHelper.ShortenSingleFile(content, settings, promptRequest, aiMustNotRun);

        // Assert
        Assert.AreEqual(1, result.UnitsWithRestrictionCount);
        Assert.AreEqual(0, result.UnitsOverLimitCount);
        Assert.AreEqual(0, result.UnitsUpdatedCount);
    }

    private static Transformation LoadInput(string name)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectDir = Directory.GetParent(baseDir)!.Parent!.Parent!.Parent!.FullName;
        using var stream = File.OpenRead(Path.Combine(projectDir, "TestFiles", "Input", name));
        var load = Transformation.Load(stream, name);
        return load.Success ? load.Value : throw new Exception(load.Error);
    }
}