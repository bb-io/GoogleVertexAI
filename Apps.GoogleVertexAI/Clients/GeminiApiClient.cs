using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace Apps.GoogleVertexAI.Clients;

public abstract class GeminiApiClient : RestClient, IGeminiApiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/";

    protected static readonly JsonSerializerSettings JsonOptions = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
    };

    private readonly Blackbird.Applications.Sdk.Common.Invocation.Logger? _logger;

    protected GeminiApiClient(
        Blackbird.Applications.Sdk.Common.Invocation.Logger? logger) : base(
            new RestClientOptions(BaseUrl),
            configureSerialization: s => s.UseNewtonsoftJson(JsonOptions))
    {
        _logger = logger;
    }

    public RestRequest CreateRequest(string resource, Method method = Method.Get)
        => new(resource, method);

    public RestRequest CreateAbsoluteRequest(string url, Method method = Method.Get)
        => new(url, method);

    public async Task<T> ExecuteAsync<T>(RestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync(request, cancellationToken);

        var result = JsonConvert.DeserializeObject<T>(response.Content ?? string.Empty, JsonOptions);
        if (result is null)
        {
            throw new PluginApplicationException("Gemini API returned an empty response.");
        }

        return result;
    }

    public new async Task<RestResponse> ExecuteAsync(RestRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(request);

        var response = await base.ExecuteAsync(request, cancellationToken);
        EnsureSuccess(request, response);

        return response;
    }

    protected abstract Task AuthorizeAsync(RestRequest request);

    private void EnsureSuccess(RestRequest request, RestResponse response)
    {
        if (response.IsSuccessful)
        {
            return;
        }

        var error = TryGetGoogleErrorMessage(response.Content);
        var resource = request.Resource ?? "<unknown-resource>";
        _logger?.LogError($"[GoogleGeminiFileSearch] Request to {resource} failed with status {response.StatusCode}: {error}", []);
        throw new PluginApplicationException($"{error} Request: {resource}");
    }

    private static string TryGetGoogleErrorMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Gemini API request failed.";
        }

        try
        {
            var envelope = JsonConvert.DeserializeObject<GeminiGoogleErrorEnvelope>(content, JsonOptions);
            return envelope?.Error?.Message ?? content;
        }
        catch
        {
            return content;
        }
    }
}
