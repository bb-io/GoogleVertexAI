using System.Globalization;
using Apps.GoogleVertexAI.Constants;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication.OAuth2;
using Blackbird.Applications.Sdk.Common.Invocation;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.GoogleVertexAI.Connections.OAuth2;

public class OAuth2TokenService : BaseInvocable, IOAuth2TokenService
{
    private const string ExpiresAtKeyName = "expires_at";
    
    public OAuth2TokenService(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public bool IsRefreshToken(Dictionary<string, string> values)
        => DateTime.UtcNow > DateTime.Parse(values[ExpiresAtKeyName], CultureInfo.InvariantCulture);
    
    public Task<Dictionary<string, string>> RequestToken(
        string state,
        string code,
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var bodyParameters = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", ApplicationConstants.ClientId },
            { "client_secret", ApplicationConstants.ClientSecret },
            { "redirect_uri", InvocationContext.UriInfo.AuthorizationCodeRedirectUri.ToString() },
            { "code", code }
        };

        return GetTokenData(bodyParameters, cancellationToken);
    }
    
    public Task<Dictionary<string, string>> RefreshToken(Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var bodyParameters = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", ApplicationConstants.ClientId },
            { "client_secret", ApplicationConstants.ClientSecret },
            { "refresh_token", values["refresh_token"] }
        };

        return GetTokenData(bodyParameters, cancellationToken);
    }

    public Task RevokeToken(Dictionary<string, string> values)
        => throw new NotImplementedException();
    
    private async Task<Dictionary<string, string>> GetTokenData(Dictionary<string, string> bodyParameters,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteTokenRequest(bodyParameters, cancellationToken);
        return ParseTokenResponse(response.Content!);
    }
    
    private Dictionary<string, string> ParseTokenResponse(string responseContent)
    {
        var resultDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);

        if (resultDictionary is null)
            throw new InvalidOperationException($"Invalid response content: {responseContent}");

        var expiresIn = int.Parse(resultDictionary["expires_in"]);
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        resultDictionary[ExpiresAtKeyName] = expiresAt.ToString(CultureInfo.InvariantCulture);
        return resultDictionary;
    }
    
    private async Task<RestResponse> ExecuteTokenRequest(Dictionary<string, string> bodyParameters,
        CancellationToken cancellationToken)
    {
        using var client = new RestClient();
        var request = new RestRequest(Urls.TokenUrl, Method.Post);
        bodyParameters.ToList().ForEach(x => request.AddParameter(x.Key, x.Value));

        return await client.ExecuteAsync(request, cancellationToken);
    }
}