using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Utils;
using Polly;
using Polly.Retry;
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

    private static readonly Regex RetryDelayPattern = new("\"retryDelay\"\\s*:\\s*\"([0-9.]+)s\"", RegexOptions.Compiled);

    private static readonly AsyncRetryPolicy<RestResponse> RateLimitPolicy = Policy
        .HandleResult<RestResponse>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            RateLimitBackoff.RetryCount,
            (retryAttempt, result, _) => GetRetryDelay(retryAttempt, result.Result),
            (_, _, _, _) => Task.CompletedTask);

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

        var response = await RateLimitPolicy.ExecuteAsync(
            token => base.ExecuteAsync(request, token),
            cancellationToken);
        EnsureSuccess(request, response);

        return response;
    }

    private static TimeSpan GetRetryDelay(int retryAttempt, RestResponse response)
    {
        var retryAfter = response.Headers?
            .FirstOrDefault(header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))?
            .Value?.ToString();

        if (int.TryParse(retryAfter, out var seconds))
        {
            return RateLimitBackoff.GetDelay(retryAttempt, seconds);
        }

        // Gemini usually sends no header but a RetryInfo detail such as "retryDelay": "23s" in the body.
        var retryDelay = RetryDelayPattern.Match(response.Content ?? string.Empty);
        return retryDelay.Success && double.TryParse(retryDelay.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var delay)
            ? RateLimitBackoff.GetDelay(retryAttempt, (int)Math.Ceiling(delay))
            : RateLimitBackoff.GetDelay(retryAttempt);
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
