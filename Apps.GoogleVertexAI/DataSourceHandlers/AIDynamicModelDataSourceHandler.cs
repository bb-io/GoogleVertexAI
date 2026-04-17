using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class AIModelDynamicDataSourceHandler(InvocationContext invocationContext)
    : VertexAiInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var models = ConnectionType switch
        {
            ConnectionTypes.GeminiApiKey => await GetGeminiApiModelsAsync(GeminiApiClient, cancellationToken),
            ConnectionTypes.ServiceAccount => await GetVertexModelsAsync(GeminiApiClient, cancellationToken),
            _ => []
        };

        return models
            .Where(x => IsSupportedTextModel(x.ModelId))
            .Where(x => MatchesSearch(x, context.SearchString))
            .DistinctBy(x => x.ModelId)
            .OrderByDescending(x => x.ModelId, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DataSourceItem(x.ModelId, x.DisplayName));
    }

    private async Task<List<AIModelDataSourceItem>> GetVertexModelsAsync(
        IGeminiApiClient client,
        CancellationToken cancellationToken)
    {
        var models = new List<AIModelDataSourceItem>();
        string? pageToken = null;

        do
        {
            var url = $"{GetVertexApiBaseUrl()}/v1beta1/publishers/google/models?pageSize=200&listAllVersions=true";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            var request = client.CreateAbsoluteRequest(url, Method.Get);
            var response = await client.ExecuteAsync<VertexPublisherModelListResponse>(request, cancellationToken);

            if (response.PublisherModels is not null)
            {
                models.AddRange(response.PublisherModels
                    .Select(MapVertexModel)
                    .Where(x => x is not null)!);
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return models;
    }

    private static async Task<List<AIModelDataSourceItem>> GetGeminiApiModelsAsync(
        IGeminiApiClient client,
        CancellationToken cancellationToken)
    {
        var models = new List<AIModelDataSourceItem>();
        string? pageToken = null;

        do
        {
            var resource = "v1beta/models?pageSize=1000";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                resource += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            var request = client.CreateRequest(resource, Method.Get);
            var response = await client.ExecuteAsync<GeminiModelListResponse>(request, cancellationToken);

            if (response.Models is not null)
            {
                models.AddRange(response.Models
                    .Select(MapGeminiApiModel)
                    .Where(x => x is not null)!);
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return models;
    }

    private static AIModelDataSourceItem? MapGeminiApiModel(GeminiApiModelResource model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return null;
        }

        var modelId = ExtractModelId(model.Name);
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        if (model.SupportedGenerationMethods is { Count: > 0 }
            && !model.SupportedGenerationMethods.Contains("generateContent", StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return new(modelId, BuildDisplayName(modelId, model.DisplayName));
    }

    private static AIModelDataSourceItem? MapVertexModel(VertexPublisherModelResource model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return null;
        }

        var modelId = ExtractModelId(model.Name);
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        return new(modelId, BuildDisplayName(modelId, model.DisplayName));
    }

    private static bool MatchesSearch(AIModelDataSourceItem item, string? searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
        {
            return true;
        }

        return item.ModelId.Contains(searchString, StringComparison.OrdinalIgnoreCase)
               || item.DisplayName.Contains(searchString, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedTextModel(string modelId)
    {
        return modelId.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase)
               && !modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase)
               && !modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
               && !modelId.Contains("live", StringComparison.OrdinalIgnoreCase)
               && !modelId.Contains("tts", StringComparison.OrdinalIgnoreCase);
    }

    private string GetVertexApiBaseUrl()
    {
        var region = Region.Trim().ToLowerInvariant();
        return region is "" or "global" or "us-central1"
            ? "https://aiplatform.googleapis.com"
            : $"https://{region}-aiplatform.googleapis.com";
    }

    private static string ExtractModelId(string resourceName)
    {
        var segments = resourceName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.LastOrDefault() ?? string.Empty;
    }

    private static string BuildDisplayName(string modelId, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName!;
        }

        var formatted = modelId
            .Replace("gemini-", "Gemini ", StringComparison.OrdinalIgnoreCase)
            .Replace("-preview-customtools", " Preview Customtools", StringComparison.OrdinalIgnoreCase)
            .Replace("-preview", " Preview", StringComparison.OrdinalIgnoreCase)
            .Replace("-flash-lite", " Flash-Lite", StringComparison.OrdinalIgnoreCase)
            .Replace("-flash", " Flash", StringComparison.OrdinalIgnoreCase)
            .Replace("-pro", " Pro", StringComparison.OrdinalIgnoreCase)
            .Replace("-", " ");

        return string.Join(" ", formatted.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record AIModelDataSourceItem(string ModelId, string DisplayName);
}
