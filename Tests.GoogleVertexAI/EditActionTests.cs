using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class EditActionTests : TestBase
{
    private const string ModelName = "gemini-2.5-flash";


    [TestMethod]
    public async Task Edit_xliff()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var translateRequest = new EditFileRequest
        {
            File = new FileReference { Name = "contentful.html.xlf" },
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var result = await actions.EditContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.File.Name.Contains("contentful"));

        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    [TestMethod]
    public async Task EditText_WithSerbianLocale_ReturnsLocalizedText()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var localizeRequest = new EditTextRequest
        {
            SourceText = "Develop and implement an HR strategy that drives organizational productivity and supports company's business goals. Design and oversee processes that promote team efficiency and operational effectiveness while reducing complexity and redundancies.",
            TargetText = "Razviti i primeniti HR strategiju koja podstiče organizacionu produktivnost i podržava poslovne ciljeve kompanije. Dizajnirati i nadgledati procese koji promovišu efikasnost tima i operativnu efektivnost, uz smanjenje kompleksnosti i redundanci.",
            TargetLanguage = "sr-Latn-RS",
            AIModel = ModelName,
        };

        var glossaryRequest = new GlossaryRequest();
        string? systemMessage = null;
        var result = await actions.EditText(localizeRequest, new PromptRequest { }, systemMessage, glossaryRequest);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.EditedText);
        Console.WriteLine("Original: " + localizeRequest.TargetText);
        Console.WriteLine("Localized: " + result.EditedText);
    }

    [TestMethod]
    public async Task Batch_edit_text_returns_valid_xliff()
    {
        var actions = new EditActions(InvocationContext, FileManager);
        var file = new FileReference { Name = "contentful.html.xlf" };
        var translateRequest = new EditFileRequest
        {
            File = file,
            AIModel = ModelName,
        };
        string? systemMessage = null;
        var glossaryRequest = new GlossaryRequest();

        var startbatchResopnse = await actions.BatchEditContent(translateRequest, new PromptRequest { }, systemMessage, glossaryRequest);
        Assert.IsNotNull(startbatchResopnse);
        Console.WriteLine(startbatchResopnse.JobName);

        var polling = new BatchPolling(InvocationContext);


        var result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>() {
            Memory = new BatchMemory
            {
                LastPollingTime = DateTime.UtcNow,
                Triggered = false
            }
        }, new BatchIdentifier { JobName = startbatchResopnse.JobName });

        while (!result.FlyBird)
        {
            await Task.Delay(3000);
            result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>()
            {
                Memory = new BatchMemory
                {
                    LastPollingTime = DateTime.UtcNow,
                    Triggered = false
                }
            }, new BatchIdentifier { JobName = startbatchResopnse.JobName });
        }

        var batchActions = new BatchActions(InvocationContext, FileManager);

        var finalResult = await batchActions.DownloadXliffFromBatch(startbatchResopnse.JobName, new GetBatchResultRequest { OriginalXliff = startbatchResopnse.TransformationFile });

        Console.WriteLine(JsonConvert.SerializeObject(finalResult, Formatting.Indented));

        Assert.IsNotNull(finalResult.File);
    }
}
