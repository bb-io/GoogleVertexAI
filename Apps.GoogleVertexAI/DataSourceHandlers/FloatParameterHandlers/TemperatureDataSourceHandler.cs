using Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers.Base;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers;

public class TemperatureDataSourceHandler : BaseFloatParameterDataSourceHandler
{
    protected override float LowerBoundary => 0.0f;
    protected override float UpperBoundary => 1.0f;

    public TemperatureDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }
}