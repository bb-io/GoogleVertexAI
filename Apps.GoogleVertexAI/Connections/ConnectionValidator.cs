using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.GoogleVertexAI.Connections;

public class ConnectionValidator : IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        CancellationToken cancellationToken)
    {
        try
        {
            var region = authenticationCredentialsProviders
                .FirstOrDefault(x => x.KeyName == CredNames.Region)?.Value ?? "global";

            var generativeModelClient = GenerativeModelClientFactory.Create(
                authenticationCredentialsProviders,
                null,
                region);

            await generativeModelClient.ValidateConnectionAsync(cancellationToken);

            return new ConnectionValidationResponse { IsValid = true };
        }
        catch (Exception ex)
        {
            return new ConnectionValidationResponse
            {
                IsValid = false,
                Message = ex.Message
            };
        }
    }
}
