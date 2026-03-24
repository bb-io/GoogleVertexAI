using RestSharp;

namespace Apps.GoogleVertexAI.Clients;

public sealed class GeminiApiKeyClient(string apiKey, Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    : GeminiApiClient(logger)
{
    protected override Task AuthorizeAsync(RestRequest request)
    {
        request.AddOrUpdateParameter("key", apiKey, ParameterType.QueryString);
        return Task.CompletedTask;
    }
}
