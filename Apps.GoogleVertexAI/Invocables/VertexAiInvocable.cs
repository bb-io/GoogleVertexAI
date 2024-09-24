using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;

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
        var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(invocationContext.AuthenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value);
        if (serviceConfig == null) throw new Exception("The service config string was not properly formatted");
        ProjectId = serviceConfig.ProjectId;
    }
}