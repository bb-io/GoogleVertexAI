using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.GoogleVertexAI.Clients;

public sealed class GeminiServiceAccountClient : GeminiApiClient
{
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/cloud-platform",
        "https://www.googleapis.com/auth/generative-language.retriever",
        "https://www.googleapis.com/auth/devstorage.read_only"
    ];

    private readonly GoogleCredential _credential;
    private readonly string _projectId;

    public GeminiServiceAccountClient(
        string serviceAccountJson,
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger) : base(logger)
    {
        var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(serviceAccountJson)
                            ?? throw new PluginApplicationException("The service config string was not properly formatted");

        _projectId = serviceConfig.ProjectId;
        _credential = GoogleCredential.FromJson(serviceAccountJson);

        if (_credential.IsCreateScopedRequired)
        {
            _credential = _credential.CreateScoped(Scopes);
        }
    }

    protected override async Task AuthorizeAsync(RestRequest request)
    {
        var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

        request.AddOrUpdateHeader("Authorization", $"Bearer {token}");
        request.AddOrUpdateHeader("x-goog-user-project", _projectId);
    }
}
