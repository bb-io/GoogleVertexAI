using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Files;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI;

[TestClass]
public class ReportingActionsTests : TestBase
{
    private ReportingActions _actions => new(InvocationContext, FileManager);

    private const string ModelName = "gemini-2.5-flash-lite";
    private const string TestFileName = "contentful.html.xlf";

    [TestMethod]
    public async Task Valid_xliff_returns_MQM_report()
    {

        // Act
        var result = await _actions.GenerateMQMReport(
            new GetTranslationIssuesRequest
            {
                File = new FileReference { Name = TestFileName },
                AIModel = ModelName,
            }, 
            new GlossaryRequest(),
            new PromptRequest
            {
                MaxOutputTokens = 2500
            }, 
            null, 
            null
            );

        // Assert
        PrintResult(result);

        Assert.IsTrue(result.Report.Length > 0);
        Assert.IsTrue(result.Usage.TotalTokens > 0);
    }
}