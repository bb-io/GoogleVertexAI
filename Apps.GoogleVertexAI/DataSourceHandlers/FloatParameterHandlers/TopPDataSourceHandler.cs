using Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers.Base;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers;

public class TopPDataSourceHandler : BaseFloatParameterDataSourceHandler
{
    protected override float LowerBoundary => 0.0f;
    protected override float UpperBoundary => 1.0f;

    public TopPDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }
}