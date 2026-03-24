using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Factories;

public static class GenerativeModelClientFactory
{
    public static IGenerativeModelClient Create(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger,
        string region)
    {
        var connectionType = credentials.FirstOrDefault(x => x.KeyName == CredNames.ConnectionType)?.Value
                             ?? ConnectionTypes.ServiceAccount;

        return connectionType switch
        {
            ConnectionTypes.GeminiApiKey => new GeminiRestGenerativeModelClient(
                GeminiApiClientFactory.Create(credentials, logger)),
            ConnectionTypes.ServiceAccount => CreateVertexClient(credentials, logger, region),
            _ => throw new PluginApplicationException($"Unsupported connection type: {connectionType}")
        };
    }

    private static IGenerativeModelClient CreateVertexClient(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger,
        string region)
    {
        var serviceAccountJson = credentials.Get(CredNames.ServiceAccountConfString).Value;
        var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(serviceAccountJson)
                            ?? throw new PluginApplicationException("The service config string was not properly formatted");

        return new VertexGenerativeModelClient(
            ClientFactory.Create(credentials, region),
            serviceAccountJson,
            serviceConfig.ProjectId,
            region,
            logger);
    }
}
