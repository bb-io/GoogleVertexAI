using Apps.GoogleVertexAI.Invocables;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.Actions;

[ActionList]
public class Actions : VertexAiInvocable
{
    public Actions(InvocationContext invocationContext) : base(invocationContext)
    {
    }
}