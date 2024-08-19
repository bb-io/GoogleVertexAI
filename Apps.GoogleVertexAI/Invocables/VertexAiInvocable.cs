using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Invocables;

public class VertexAiInvocable : BaseInvocable
{
    protected readonly PredictionServiceClient Client;
    protected readonly string ProjectId;

    protected AuthenticationCredentialsProvider[] Creds =>
        InvocationContext.AuthenticationCredentialsProviders.ToArray();

    protected VertexAiInvocable(InvocationContext invocationContext) : base(invocationContext)
    {
        Client = ClientFactory.Create(invocationContext.AuthenticationCredentialsProviders);
        ProjectId = invocationContext.AuthenticationCredentialsProviders.Get(CredNames.ProjectId).Value;
    }
}