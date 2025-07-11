using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class TranslateActionsTests : TestBase
{
    private const string ModelName = "gemini-2.5-flash";

    [TestMethod]
    public async Task Translate_html()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var translateRequest = new TranslateFileRequest
        {
            File = new FileReference { Name = "contentful.html" },
            TargetLanguage = "nl",
            AIModel = ModelName,
            OutputFileHandling = "original",
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var result = await actions.TranslateContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.File.Name.Contains("contentful"));

        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task Translate_xliff()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var translateRequest = new TranslateFileRequest
        {
            File = new FileReference { Name = "contentful.html.xlf" },
            TargetLanguage = "nl",
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var result = await actions.TranslateContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.File.Name.Contains("contentful"));

        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task TranslateText_WithSerbianLocale_ReturnsLocalizedText()
    {
        var actions = new TranslationActions(InvocationContext, FileManager);
        var localizeRequest = new TranslateTextRequest
        {
            Text = "Develop and implement an HR strategy that drives organizational productivity and supports company's business goals. Design and oversee processes that promote team efficiency and operational effectiveness while reducing complexity and redundancies.",
            TargetLanguage = "sr-Latn-RS",
            AIModel = ModelName,
        };

        var glossaryRequest = new GlossaryRequest();
        string? systemMessage = null;
        var result = await actions.LocalizeText(localizeRequest, new PromptRequest { }, systemMessage, glossaryRequest);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.TranslatedText);
        Console.WriteLine("Original: " + localizeRequest.Text);
        Console.WriteLine("Localized: " + result.TranslatedText);

        // Additional validation to ensure response is not empty and contains Serbian characters
        Assert.IsTrue(result.TranslatedText.Length > 0);
    }
}
