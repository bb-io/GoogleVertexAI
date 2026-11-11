using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;
using System.Net.Mime;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiGenerateActionsTests : TestBase
{
    private const string ModelName = "gemini-3.8-flash";
    private const string PublicYouTubeVideoUrl = "https://www.youtube.com/watch?v=9hE5-98ZeCg";

    private GeminiGenerateActions Actions => new(InvocationContext, FileManager);

    [TestMethod]
    public async Task GenerateText_WithValidPrompt_ReturnsNonEmptyResponse()
    {
        var request = new GenerateTextRequest
        {
            AIModel = ModelName,
            Prompt = "Explain in one sentence what is sun?"
        };

        var response = await Actions.GenerateText(request, new PromptRequest());

        Console.WriteLine(response.GeneratedText);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }

    [TestMethod]
    public async Task GenerateText_WithYouTubeVideoUrl_DescribesAudioAndVisualsWithTimestamps()
    {
        var request = new GenerateTextRequest
        {
            AIModel = ModelName,
            Prompt = "List three moments of this video with MM:SS timestamps. For each moment quote what is said, " +
                     "name who is speaking if it can be identified, and transcribe any on-screen text.",
            YouTubeVideoUrl = PublicYouTubeVideoUrl
        };

        var response = await Actions.GenerateText(request, new PromptRequest());

        Console.WriteLine(response.GeneratedText);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
        Assert.IsTrue(response.Usage.PromptTokens > 1000, $"Prompt tokens: {response.Usage.PromptTokens}");
    }

    [TestMethod]
    public async Task GenerateTextFromFile_WithValidFileAndPrompt_ReturnsNonEmptyResponse()
    {
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

        var response = await Actions.GenerateTextFromFile(request, new PromptRequest());

        Console.WriteLine(response.GeneratedText);
        Assert.IsFalse(string.IsNullOrEmpty(response.GeneratedText));
    }
}
