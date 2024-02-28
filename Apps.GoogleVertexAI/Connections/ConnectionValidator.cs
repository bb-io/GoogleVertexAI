using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Connections;

public class ConnectionValidator : IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        CancellationToken cancellationToken)
    {
        var client = ClientFactory.Create(authenticationCredentialsProviders);

        try
        {
            var projectId = authenticationCredentialsProviders.Get(CredNames.ProjectId).Value;
            var endpoint = EndpointName
                .FromProjectLocationPublisherModel(projectId, Urls.Location, PublisherIds.Google, ModelIds.GeminiPro)
                .ToString();
            
            var content = new Content
            {
                Role = "USER",
                Parts = { new Part { Text = "Ping" } }
            };

            var generateContentRequest = new GenerateContentRequest
            {
                Model = endpoint,
                GenerationConfig = new GenerationConfig { MaxOutputTokens = 10 }
            };
            generateContentRequest.Contents.Add(content);

            using var response = client.StreamGenerateContent(generateContentRequest);
            var responseStream = response.GetResponseStream();
            
            await foreach (var responseItem in responseStream)
            {
            }
            
            return new()
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            return new()
            {
                IsValid = false,
                Message = ex.Message
            };
        }
    }
}