using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dtos;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.GoogleVertexAI.Api;

public class VertexAiClient : BlackBirdRestClient
{
    protected override JsonSerializerSettings JsonSettings =>
        new() { MissingMemberHandling = MissingMemberHandling.Ignore };

    public VertexAiClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
        : base(new RestClientOptions
            { ThrowOnAnyError = false, BaseUrl = GetBaseUri(authenticationCredentialsProviders) })
    {
        var accessToken = authenticationCredentialsProviders.First(p => p.KeyName == CredNames.AccessToken).Value;
        this.AddDefaultHeader("Authorization", $"Bearer {accessToken}");
    }

    protected override Exception ConfigureErrorException(RestResponse response)
    {
        if (response.Content == null)
            return new(response.StatusCode.ToString());

        var error = JsonConvert.DeserializeObject<IEnumerable<ErrorDtoWrapper>>(response.Content, JsonSettings)!.First()
            .Error;
        
        return new($"Error code {error.Code}: {error.Message}");
    }

    private static Uri GetBaseUri(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    {
        var projectId = authenticationCredentialsProviders.First(p => p.KeyName == CredNames.ProjectId).Value;
        return new(string.Format(Urls.ApiUrl, projectId));
    }
}