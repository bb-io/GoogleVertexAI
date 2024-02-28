using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers.Base;

public abstract class BaseFloatParameterDataSourceHandler : BaseInvocable, IDataSourceHandler
{
    protected abstract float LowerBoundary { get; }
    protected abstract float UpperBoundary { get; }
    
    protected BaseFloatParameterDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }
    
    public Dictionary<string, string> GetData(DataSourceContext context)
    {
        var parameters = GenerateFormattedFloatArray()
            .Where(parameter => context.SearchString == null || parameter.Contains(context.SearchString))
            .ToDictionary(parameter => parameter, parameter => parameter);

        return parameters;
    }
    
    private string[] GenerateFormattedFloatArray()
    {
        const float step = 0.1f;
        
        var length = (int)Math.Ceiling((UpperBoundary - LowerBoundary) / step) + 1;
        var result = new string[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = (LowerBoundary + i * step).ToString("0.0");
        }

        return result;
    }
}