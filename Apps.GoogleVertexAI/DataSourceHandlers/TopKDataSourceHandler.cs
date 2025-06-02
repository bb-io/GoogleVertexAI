using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class TopKDataSourceHandler(InvocationContext invocationContext) : BaseInvocable(invocationContext), IDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData(DataSourceContext context)
    {
        var parameters = GenerateTopKValuesArray()
            .Where(parameter => context.SearchString == null || parameter.Contains(context.SearchString))
            .Select(parameter => new DataSourceItem(parameter, parameter))
            .ToList();

        return parameters;
    }

    private string[] GenerateTopKValuesArray()
    {
        const int step = 1;
        const int lowerBoundary = 1;
        const int upperBoundary = 40;

        var length = (int)Math.Ceiling((upperBoundary - lowerBoundary) / (double)step) + 1;
        var result = new string[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = (lowerBoundary + i * step).ToString();
        }

        return result;
    }
}