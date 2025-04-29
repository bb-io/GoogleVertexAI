using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Factories;

public static class ClientFactory
{
    public static PredictionServiceClient Create(IEnumerable<AuthenticationCredentialsProvider> credentials,string region)
    {
        var jsonConfiguration = credentials.Get(CredNames.ServiceAccountConfString).Value;

        var apiUrl = region.Equals("global", StringComparison.OrdinalIgnoreCase)? "https://aiplatform.googleapis.com"
            : $"https://{region}-aiplatform.googleapis.com";

        return new PredictionServiceClientBuilder
        {
            JsonCredentials = jsonConfiguration,
            Endpoint = apiUrl
        }.Build();
    }
}