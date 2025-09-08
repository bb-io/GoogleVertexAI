using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using DocumentFormat.OpenXml.Wordprocessing;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiXliffActionsTests : TestBase
{
    private const string ModelName = "gemini-2.5-pro";
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
    public async Task TranslateXliff_WithNonV12_ThrowsException()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);
        var translationRequest = new TranslateXliffRequest
        {
            File = new FileReference { Name = "contentful.html.xlf" },
            AIModel = ModelName
        };
        var customPrompt = "Prompt placeholder";
        var modelRequest = new PromptRequest { };
        var glossaryRequest = new GlossaryRequest();

        // Act & Assert
        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(async () =>
        {
            await action.TranslateXliff(translationRequest, modelRequest, customPrompt, glossaryRequest, bucketSize: 100);
        });
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



    [TestMethod]
    public async Task StartXliffBatchTranslation_WithValidInputs_ReturnsTranslatedDocument()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);

        var translationRequest = new TranslateXliffRequest
        {
            File = new FileReference { Name = TestFileName },
            AIModel = "gemini-2.5-flash"
        };

        var customPrompt = "You are a human translator native in the target language identified in the file. Translate the text from the source language identified in the file to the target language identified in the file. Ensure that any tags included in the source language are replicated in the target language. Ensure the output is provided in valid XML/XLIFF format, similar to the input file format.";
        var promtlRequest = new PromptRequest { };
        var glossaryRequest = new GlossaryRequest();

        // Act
        var result = await action.StartXliffBatchTranslation(translationRequest, promtlRequest, customPrompt, glossaryRequest);

        // Assert
        Assert.IsNotNull(result);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task StartPostEditXliffBatchTranslation_WithValidInputs_ReturnsTranslatedDocument()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);

        var translationRequest = new PostEditXliffRequest
        {
            File = new FileReference { Name = TestFileName },
            AIModel = "gemini-2.5-flash"
        };

        var customPrompt = "You are a human translator native in the target language identified in the file. Translate the text from the source language identified in the file to the target language identified in the file. Ensure that any tags included in the source language are replicated in the target language. Ensure the output is provided in valid XML/XLIFF format, similar to the input file format.";
        var promtlRequest = new PromptRequest { };
        var glossaryRequest = new GlossaryRequest();

        // Act
        var result = await action.StartXliffPostEditBatch(translationRequest, customPrompt, glossaryRequest, promtlRequest);

        // Assert
        Assert.IsNotNull(result);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task GetXliffBatchStatus_WithValidInputs_ReturnsTranslatedDocument()
    {
        // Arrange
        var action = new BatchPolling(InvocationContext);

        var batchIdentifier = new BatchIdentifier
        {
            JobName = "projects/1005036224929/locations/us-central1/batchPredictionJobs/1723507927060316160",
            
        };
        var memory = new BatchMemory
        {
            LastPollingTime = DateTime.UtcNow,
            Triggered = false
        };
        var request = new PollingEventRequest<BatchMemory>
        {
            Memory = memory
        };
        // Act
        var result = await action.OnBatchFinished(request, batchIdentifier);

        // Assert
        Assert.IsNotNull(result);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task DownloadXliffFromBatch_WithValidInputs_ReturnsTranslatedDocument()
    {
        // Arrange
        var action = new GeminiXliffActions(InvocationContext, FileManager);
        var file = new FileReference { Name = TestFileName };

        var originalfile = new GetBatchResultRequest
        {
            OriginalXliff = file
        };
        // Act
        var result = await action.DownloadXliffFromBatch("projects/1005036224929/locations/us-central1/batchPredictionJobs/3097387288385028096", originalfile);

        // Assert
        Assert.IsNotNull(result);
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }
}
