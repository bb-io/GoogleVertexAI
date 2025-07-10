using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiXliffActionsTests : TestBase
{
    private const string ModelName = "gemini-2.5-pro-exp-03-25";
    private const string TestFileName = "test.xliff";
    
    [TestMethod]
    public async Task ScoreXLIFF_WithValidInputs_ReturnsScoreResult()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);
        
        var scoreRequest = new ScoreXliffRequest 
        {
            File = new FileReference { Name = TestFileName },
            AIModel = ModelName
        };
        
        var instructionsPrompt = "You are a human translator native in the target language identified in the file. Translate the text from the source language identified in the file to the target language identified in the file. Ensure that any tags included in the source language are replicated in the target language. Ensure the output is provided in valid XML/XLIFF format, similar to the input file format.";
            
        var modelRequest = new PromptRequest {  };
        
        // Act
        var result = await action.ScoreXLIFF(scoreRequest, instructionsPrompt, modelRequest);
        
        // Assert
        Console.WriteLine(result.AverageScore);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.AverageScore >= 0);
    }   
    
    [TestMethod]
    public async Task TranslateXliff_WithValidInputs_ReturnsTranslatedDocument()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);

        var translationRequest = new TranslateXliffRequest
        {
            File = new FileReference { Name = TestFileName },
            AIModel = ModelName
        };

        var customPrompt = "You are a human translator native in the target language identified in the file. Translate the text from the source language identified in the file to the target language identified in the file. Ensure that any tags included in the source language are replicated in the target language. Ensure the output is provided in valid XML/XLIFF format, similar to the input file format.";
        var modelRequest = new PromptRequest { };
        var glossaryRequest = new GlossaryRequest();

        // Act
        var result = await action.TranslateXliff(translationRequest, modelRequest, customPrompt, glossaryRequest, bucketSize: 100);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.File);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }
    
    [TestMethod]
    public async Task GetTranslationIssues_WithValidInputs_ReturnsIssuesAnalysis()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);
        
        var issuesRequest = new GetTranslationIssuesRequest
        {
            File = new FileReference { Name = TestFileName },
            AIModel = ModelName,
            TargetAudience = "General public"
        };
        
        var modelRequest = new PromptRequest {};
        var glossaryRequest = new GlossaryRequest();
        
        // Act
        var result = await action.GetTranslationIssues(
            issuesRequest,
            glossaryRequest,
            modelRequest,
            null,
            bucketSize: 15);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Issues);
        Assert.IsTrue(result.Issues.Length > 0);
        Assert.IsNotNull(result.Usage);
        Console.WriteLine($"Issues analysis length: {result.TranslationIssues.Count}");
        Console.WriteLine(JsonConvert.SerializeObject(result.TranslationIssues, Formatting.Indented));
    }
}
