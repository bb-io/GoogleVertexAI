using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class FileSearchStoreDataSourceHandler(InvocationContext invocationContext) : IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var client = GeminiApiClientFactory.Create(
            invocationContext.AuthenticationCredentialsProviders,
            invocationContext.Logger);

        var stores = await GetStoresAsync(client);

        return stores
            .Where(x => MatchesSearch(x, context.SearchString))
            .Select(x => new DataSourceItem(
                x.Name!,
                string.IsNullOrWhiteSpace(x.DisplayName)
                    ? x.Name!
                    : $"{x.DisplayName} ({x.Name})"));
    }

    private static bool MatchesSearch(GeminiFileSearchStoreResource store, string? searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
        {
            return true;
        }

        return (!string.IsNullOrWhiteSpace(store.Name) && store.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase))
               || (!string.IsNullOrWhiteSpace(store.DisplayName)
                   && store.DisplayName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<GeminiFileSearchStoreResource>> GetStoresAsync(IGeminiApiClient client)
    {
        var stores = new List<GeminiFileSearchStoreResource>();
        string? pageToken = null;

        do
        {
            var resource = "v1beta/fileSearchStores?pageSize=20";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                resource += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            var request = client.CreateRequest(resource, Method.Get);
            var response = await client.ExecuteAsync<GeminiFileSearchStoreListResponse>(request);
            if (response.FileSearchStores is not null)
            {
                stores.AddRange(response.FileSearchStores
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name)));
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return stores;
    }
}
