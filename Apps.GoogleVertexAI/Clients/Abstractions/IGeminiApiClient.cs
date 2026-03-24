using RestSharp;

namespace Apps.GoogleVertexAI.Clients.Abstractions;

public interface IGeminiApiClient
{
    RestRequest CreateRequest(string resource, Method method = Method.Get);

    RestRequest CreateAbsoluteRequest(string url, Method method = Method.Get);

    Task<T> ExecuteAsync<T>(RestRequest request, CancellationToken cancellationToken = default);

    Task<RestResponse> ExecuteAsync(RestRequest request, CancellationToken cancellationToken = default);
}
