using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class TopKDataSourceHandler : BaseInvocable, IDataSourceHandler
{
    public TopKDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }
    
    public Dictionary<string, string> GetData(DataSourceContext context)
    {
        var parameters = GenerateTopKValuesArray()
            .Where(parameter => context.SearchString == null || parameter.Contains(context.SearchString))
            .ToDictionary(parameter => parameter, parameter => parameter);

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