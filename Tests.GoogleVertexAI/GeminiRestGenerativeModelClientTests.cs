using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Tests.GoogleVertexAI;

[TestClass]
public class GeminiRestGenerativeModelClientTests
{
    private const string Model = "gemini-2.5-flash";
    private const string Prompt = "Summarize the video in one sentence.";
    private const string YouTubeVideoUrl = "https://www.youtube.com/watch?v=9hE5-98ZeCg";

    private readonly List<RestRequest> _sentRequests = [];

    [TestMethod]
    public async Task GenerateAsync_WithYouTubeVideoPart_SendsVideoAsFileDataBeforePrompt()
    {
        var videoPart = new Part { FileData = new FileData { FileUri = YouTubeVideoUrl, MimeType = "video/mp4" } };

        var generated = await CreateClient().GenerateAsync(new PromptRequest(), Model, Prompt, files: [videoPart]);

        var parts = SentParts();
        Assert.AreEqual("Generated answer", generated.Text);
        Assert.AreEqual(2, parts.Count);
        Assert.AreEqual(YouTubeVideoUrl, parts[0]["fileData"]?["fileUri"]?.Value<string>());
        Assert.AreEqual("video/mp4", parts[0]["fileData"]?["mimeType"]?.Value<string>());
        Assert.IsNull(parts[0]["text"]);
        Assert.AreEqual(Prompt, parts[1]["text"]?.Value<string>());
        Assert.IsNull(parts[1]["fileData"]);
    }

    [TestMethod]
    public async Task GenerateAsync_WithoutFiles_SendsOnlyPromptPart()
    {
        await CreateClient().GenerateAsync(new PromptRequest(), Model, Prompt);

        var parts = SentParts();
        Assert.AreEqual(1, parts.Count);
        Assert.AreEqual(Prompt, parts[0]["text"]?.Value<string>());
        Assert.IsNull(parts[0]["fileData"]);
    }

    [TestMethod]
    public async Task GenerateAsync_WithInlineFile_ThrowsServiceAccountRequired()
    {
        var inlineFile = new Part
        {
            InlineData = new Blob { Data = ByteString.CopyFromUtf8("content"), MimeType = "text/plain" }
        };

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(
            () => CreateClient().GenerateAsync(new PromptRequest(), Model, Prompt, files: [inlineFile]));

        StringAssert.Contains(exception.Message, "service account connection");
        Assert.IsEmpty(_sentRequests);
    }

    private GeminiRestGenerativeModelClient CreateClient()
    {
        var apiClient = new Mock<IGeminiApiClient>();
        apiClient
            .Setup(x => x.CreateRequest(It.IsAny<string>(), It.IsAny<Method>()))
            .Returns((string resource, Method method) => new RestRequest(resource, method));
        apiClient
            .Setup(x => x.ExecuteAsync<GeminiGenerateContentResponse>(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Callback((RestRequest request, CancellationToken _) => _sentRequests.Add(request))
            .ReturnsAsync(new GeminiGenerateContentResponse
            {
                Candidates =
                [
                    new GeminiCandidate
                    {
                        Content = new GeminiContent { Parts = [new GeminiPart { Text = "Generated answer" }] }
                    }
                ]
            });

        return new GeminiRestGenerativeModelClient(apiClient.Object);
    }

    private List<JToken> SentParts()
    {
        var body = _sentRequests.Single().Parameters.OfType<BodyParameter>().Single().Value!;
        return JObject.FromObject(body)["contents"]![0]!["parts"]!.ToList();
    }
}
