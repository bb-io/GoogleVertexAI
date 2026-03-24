using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.GoogleVertexAI.Factories;

public static class GeminiApiClientFactory
{
    public static IGeminiApiClient Create(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    {
        var connectionType = credentials.FirstOrDefault(x => x.KeyName == CredNames.ConnectionType)?.Value
                             ?? ConnectionTypes.ServiceAccount;

        return connectionType switch
        {
            ConnectionTypes.GeminiApiKey => CreateApiKeyClient(credentials, logger),
            ConnectionTypes.ServiceAccount => CreateServiceAccountClient(credentials, logger),
            _ => throw new PluginApplicationException($"Unsupported connection type: {connectionType}")
        };
    }

    private static IGeminiApiClient CreateApiKeyClient(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    {
        var apiKey = credentials.FirstOrDefault(x => x.KeyName == CredNames.ApiKey)?.Value;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new PluginApplicationException("Gemini API key is required.");
        }

        return new GeminiApiKeyClient(apiKey, logger);
    }

    private static IGeminiApiClient CreateServiceAccountClient(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    {
        var serviceAccountJson = credentials.FirstOrDefault(x => x.KeyName == CredNames.ServiceAccountConfString)?.Value;
        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            throw new PluginApplicationException("Service account configuration string is required.");
        }

        return new GeminiServiceAccountClient(serviceAccountJson, logger);
    }
}
