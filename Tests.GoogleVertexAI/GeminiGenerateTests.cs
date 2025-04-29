using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;
using System.Net.Mime;

namespace Tests.GoogleVertexAI
{
    [TestClass]
    public class GeminiGenerateTests : TestBase
    {
        [TestMethod]
        public async Task GenerateText_ReturnsValue()
        {
            var action = new GeminiGenerateActions(InvocationContext, FileManager);

            var response = await action.GenerateText(new GenerateTextRequest { AIModel = "gemini-2.0-flash-lite", Prompt = "Explain what is sun?" });

            Console.WriteLine(response.GeneratedText);
            Assert.IsNotNull(response);
        }

        [TestMethod]
        public async Task GenerateTextFromFile_ReturnsValue()
        {
            var action = new GeminiGenerateActions(InvocationContext, FileManager);

            var response = await action.GenerateTextFromFile(
                new GenerateTextFromFileRequest { AIModel = "gemini-2.5-pro-exp-03-25",
                File= new FileReference {Name= "test.xliff", ContentType = MediaTypeNames.Text.Xml },
                Prompt="what is that file about"});

            Console.WriteLine(response.GeneratedText);
            Assert.IsNotNull(response);
        }


    }
}
