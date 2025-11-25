using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;
using System.Net.Mime;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiGenerateActionsTests : TestBase
{
    private const string ModelName = "gemini-3-pro-preview";

    private GeminiGenerateActions Actions => new(InvocationContext, FileManager);

    [TestMethod]
    public async Task GenerateText_WithValidPrompt_ReturnsNonEmptyResponse()
    {
        // Arrange
        var request = new GenerateTextRequest 
        { 
            AIModel = ModelName, 
            Prompt = "Explain in one sentence what is sun?" 
        };

        // Act
        var response = await Actions.GenerateText(request, new PromptRequest());
        
        // Assert
        Console.WriteLine(response.GeneratedText);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }

    [TestMethod]
    public async Task GenerateTextFromFile_WithValidFileAndPrompt_ReturnsNonEmptyResponse()
    {
        // Arrange
        var fileReference = new FileReference
        {
            Name = "test.xliff", 
            ContentType = MediaTypeNames.Text.Xml
        };
        
        var request = new GenerateTextFromFileRequest 
        { 
            AIModel = ModelName,
            Files = [fileReference],
            Prompt = "What is that file about? Explain in one sentence"
        };

        // Act
        var response = await Actions.GenerateTextFromFile(request, new PromptRequest());
        
        // Assert
        Console.WriteLine(response.GeneratedText);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }
}
