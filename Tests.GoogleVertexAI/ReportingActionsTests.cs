using Apps.GoogleVertexAI.Actions;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Polling;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Polling;
using GoogleVertexAI.Base;
using Newtonsoft.Json;

namespace Tests.GoogleVertexAI;

[TestClass]
public class ReportingActionsTests : TestBase
{
    private ReportingActions _actions => new(InvocationContext, FileManager);

    private const string ModelName = "gemini-2.5-flash-lite";
    private const string TestFileName = "contentful_2.xlf";

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
                
            }, 
            null, 
            null
            );

        // Assert
        PrintResult(result);

        Assert.IsTrue(result.Report.Length > 0);
        Assert.IsTrue(result.Usage.TotalTokens > 0);
    }

    [TestMethod]
    public async Task Batch_MQM_report_returns()
    {
        var startBatchresponse = await _actions.GenerateMQMReportInBackground(
            new GetTranslationIssuesRequest
            {
                File = new FileReference { Name = TestFileName },
                AIModel = ModelName,
            },
            new GlossaryRequest(),
            new PromptRequest
            {

            },
            null,
            null
            );

        Assert.IsNotNull(startBatchresponse);
        Console.WriteLine(startBatchresponse.JobName);

        var polling = new BatchPolling(InvocationContext);


        var result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>()
        {
            Memory = new BatchMemory
            {
                LastPollingTime = DateTime.UtcNow,
                Triggered = false
            }
        }, new BatchIdentifier { JobName = startBatchresponse.JobName });

        while (!result.FlyBird)
        {
            await Task.Delay(3000);
            result = await polling.OnBatchFinished(new PollingEventRequest<BatchMemory>() { Memory = result.Memory }, new BatchIdentifier { JobName = startBatchresponse.JobName });
        }

        var batchActions = new BatchActions(InvocationContext, FileManager);

        var finalResult = await batchActions.GetBackgroundResult(startBatchresponse.JobName);

        Console.WriteLine(JsonConvert.SerializeObject(finalResult, Formatting.Indented));

        Assert.IsTrue(finalResult.Result.Length > 0);
    }
}