using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI;

[TestClass]
public class FileSearchActionsTests : TestBase
{
    private const string ModelName = "gemini-2.5-flash";

    private FileSearchActions FileSearchActions => new(InvocationContext, FileManager);

    private GeminiGenerateActions GenerateActions => new(InvocationContext, FileManager);

    [TestMethod]
    public async Task FileSearchFlow_WorksEndToEnd()
    {
        var storeName = string.Empty;

        try
        {
            var store = await FileSearchActions.CreateFileSearchStore(new CreateFileSearchStoreRequest
            {
                DisplayName = $"bb-file-search-{Guid.NewGuid():N}"
            });

            storeName = store.StoreName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(storeName));

            await FileSearchActions.UploadFileToStore(new UploadFileToStoreRequest
            {
                StoreName = storeName,
                File = await FileManager.UploadTestFileAsync("file-search-sample.txt"),
                DisplayName = "file-search-sample"
            });

            var searchResponse = await FileSearchActions.SearchDocuments(
                new SearchDocumentsRequest
                {
                    AIModel = ModelName,
                    Query = "What is the verification code? Respond with the code only.",
                    FileSearchStoreNames = [storeName]
                },
                new PromptRequest());

            Assert.IsTrue(searchResponse.GeneratedText.Contains("47291"));
            Assert.IsTrue(searchResponse.RetrievedContexts.Count > 0);

            var generateResponse = await GenerateActions.GenerateText(
                new GenerateTextRequest
                {
                    AIModel = ModelName,
                    Prompt = "What is the verification code? Respond with the code only.",
                    FileSearchStoreNames = [storeName]
                },
                new PromptRequest());

            Assert.IsTrue(generateResponse.GeneratedText.Contains("47291"));
            Assert.IsTrue(generateResponse.RetrievedContexts.Count > 0);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(storeName))
            {
                await FileSearchActions.DeleteFileSearchStore(new DeleteFileSearchStoreRequest
                {
                    StoreName = storeName,
                    Force = true
                });
            }
        }
    }
}
