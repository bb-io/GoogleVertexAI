using Apps.GoogleVertexAI.Api;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Parameters.Gemini;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using RestSharp;

namespace Apps.GoogleVertexAI.Connections;

public class ConnectionValidator: IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        CancellationToken cancellationToken)
    {
        var client = new VertexAiClient(authenticationCredentialsProviders);
        var pingRequest =
            new VertexAiRequest(string.Format(Endpoints.GeminiGenerateContent, ModelIds.GeminiPro), Method.Post)
                .WithJsonBody(new GeminiParameters(new[] { new PromptData("Ping") }, new(MaxOutputTokens: 10)));

        try
        {
            await client.ExecuteWithErrorHandling(pingRequest);

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