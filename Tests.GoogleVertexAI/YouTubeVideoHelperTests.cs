using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Newtonsoft.Json.Linq;

namespace Tests.GoogleVertexAI;

[TestClass]
public class YouTubeVideoHelperTests
{
    private const string Model = "gemini-2.5-flash";
    private const string Prompt = "Summarize the video in one sentence.";
    private const string WatchUrl = "https://www.youtube.com/watch?v=9hE5-98ZeCg";

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ToVertexParts_WithoutUrl_ReturnsNull(string? youTubeVideoUrl)
    {
        Assert.IsNull(YouTubeVideoHelper.ToVertexParts(youTubeVideoUrl));
        Assert.IsNull(YouTubeVideoHelper.ToGeminiPart(youTubeVideoUrl));
    }

    [TestMethod]
    public void ToVertexParts_WithWatchUrl_ReturnsSingleVideoFileDataPart()
    {
        var parts = YouTubeVideoHelper.ToVertexParts(WatchUrl);

        Assert.IsNotNull(parts);
        Assert.AreEqual(1, parts.Count);
        Assert.AreEqual(WatchUrl, parts[0].FileData.FileUri);
        Assert.AreEqual("video/mp4", parts[0].FileData.MimeType);
        Assert.IsNull(parts[0].InlineData);
    }

    [TestMethod]
    public void ToGeminiPart_WithWatchUrl_SerializesAsFileDataWithoutText()
    {
        var part = JObject.FromObject(YouTubeVideoHelper.ToGeminiPart(WatchUrl)!);

        Assert.AreEqual(WatchUrl, part["fileData"]?["fileUri"]?.Value<string>());
        Assert.AreEqual("video/mp4", part["fileData"]?["mimeType"]?.Value<string>());
        Assert.IsNull(part["text"]);
        Assert.IsNull(part["inlineData"]);
    }

    [TestMethod]
    [DataRow("  https://www.youtube.com/watch?v=9hE5-98ZeCg  ", WatchUrl)]
    [DataRow("https://youtube.com/watch?v=9hE5-98ZeCg", "https://youtube.com/watch?v=9hE5-98ZeCg")]
    [DataRow("https://m.youtube.com/watch?v=9hE5-98ZeCg", "https://m.youtube.com/watch?v=9hE5-98ZeCg")]
    [DataRow("https://youtu.be/9hE5-98ZeCg", WatchUrl)]
    [DataRow("https://youtu.be/9hE5-98ZeCg?si=share-token", WatchUrl)]
    public void NormalizeUrl_WithYouTubeLink_ReturnsUrlAcceptedByGemini(string youTubeVideoUrl, string expectedUrl)
    {
        Assert.AreEqual(expectedUrl, YouTubeVideoHelper.NormalizeUrl(youTubeVideoUrl));
    }

    [TestMethod]
    [DataRow("9hE5-98ZeCg")]
    [DataRow("www.youtube.com/watch?v=9hE5-98ZeCg")]
    [DataRow("ftp://www.youtube.com/watch?v=9hE5-98ZeCg")]
    [DataRow("https://example.com/watch?v=9hE5-98ZeCg")]
    [DataRow("https://notyoutube.com/watch?v=9hE5-98ZeCg")]
    [DataRow("https://youtu.be/")]
    public void NormalizeUrl_WithNonYouTubeLink_ThrowsMisconfiguration(string youTubeVideoUrl)
    {
        var exception = Assert.ThrowsExactly<PluginMisconfigurationException>(
            () => YouTubeVideoHelper.NormalizeUrl(youTubeVideoUrl));

        StringAssert.Contains(exception.Message, youTubeVideoUrl);
    }

    [TestMethod]
    public void VertexBuildRequest_WithYouTubeVideoParts_AddsVideoFileDataToUserContent()
    {
        var request = VertexGenerativeModelClient.BuildRequest(
            new PromptRequest(),
            VertexGenerativeModelClient.BuildModelResourceName("project-1", "global", Model),
            Model,
            Prompt,
            null,
            null,
            YouTubeVideoHelper.ToVertexParts(WatchUrl));

        var userParts = request.Contents.Single().Parts;
        Assert.AreEqual(2, userParts.Count);
        Assert.AreEqual(Prompt, userParts[0].Text);
        Assert.AreEqual(WatchUrl, userParts[1].FileData.FileUri);
        Assert.AreEqual("video/mp4", userParts[1].FileData.MimeType);
    }
}
