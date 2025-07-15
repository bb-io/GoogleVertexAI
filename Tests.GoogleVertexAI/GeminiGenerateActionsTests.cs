using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;
using System.Net.Mime;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiGenerateActionsTests : TestBase
{
    private const string ModelName = "gemini-2.5-pro";
    
    [TestMethod]
    public async Task GenerateText_WithValidPrompt_ReturnsNonEmptyResponse()
    {
        // Arrange
        var action = new GeminiGenerateActions(InvocationContext, FileManager);
        var request = new GenerateTextRequest 
        { 
            AIModel = ModelName, 
            Prompt = "Explain what is sun?" 
        };

        // Act
        var response = await action.GenerateText(request);
        
        // Assert
        Console.WriteLine(response.GeneratedText);
        Assert.IsNotNull(response);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }

    [TestMethod]
    public async Task GenerateTextFromFile_WithValidFileAndPrompt_ReturnsNonEmptyResponse()
    {
        // Arrange
        var action = new GeminiGenerateActions(InvocationContext, FileManager);
        var fileReference = new FileReference
        {
            Name = "test.xliff", 
            ContentType = MediaTypeNames.Text.Xml
        };
        
        var request = new GenerateTextFromFileRequest 
        { 
            AIModel = ModelName,
            File = fileReference,
            Prompt = "what is that file about"
        };

        // Act
        var response = await action.GenerateTextFromFile(request);
        
        // Assert
        Console.WriteLine(response.GeneratedText);
        Assert.IsNotNull(response);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }
}
