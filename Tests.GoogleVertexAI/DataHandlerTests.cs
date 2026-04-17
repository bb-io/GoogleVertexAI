using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI;

[TestClass]
public class DataHandlerTests : TestBase
{
    [TestMethod]
    public async Task Models_Handler_Is_Success()
    {
        var handler = new AIModelDynamicDataSourceHandler(InvocationContext);
        var dataSourceContext = new DataSourceContext
        {
           
        };
        var result = await handler.GetDataAsync(dataSourceContext, CancellationToken.None);

        foreach (var item in result)
        {
            Console.WriteLine($"ModelId: {item.Value}, DisplayName: {item.DisplayName}");
        }

        Assert.IsNotNull(result);
    }
}
