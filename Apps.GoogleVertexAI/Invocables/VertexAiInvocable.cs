using Apps.GoogleVertexAI.Api;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.Invocables;

public class VertexAiInvocable : BaseInvocable
{
    protected VertexAiClient Client { get; }
    
    protected VertexAiInvocable(InvocationContext invocationContext) : base(invocationContext)
    {
        Client = new(InvocationContext.AuthenticationCredentialsProviders);
    }
}