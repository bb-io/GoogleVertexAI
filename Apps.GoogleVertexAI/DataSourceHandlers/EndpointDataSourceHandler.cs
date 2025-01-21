using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Invocables;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class EndpointDataSourceHandler : VertexAiInvocable, IAsyncDataSourceHandler
{
    public EndpointDataSourceHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<Dictionary<string, string>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var endpointService = new EndpointServiceClientBuilder()
        {
            JsonCredentials = Creds.Get(CredNames.ServiceAccountConfString).Value,
            Endpoint = Urls.ApiUrl
        }.Build();        

        var res = endpointService.ListEndpointsAsync($"projects/{ProjectId}/locations/{Urls.Location}");
        var result = new List<Endpoint>();
        await foreach (var endpoint in res)
        {
            result.Add(endpoint);
        }

        return result
            .Append(new()
            {
                Name = EndpointName
                    .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google,
                        ModelIds.GeminiProFlash).ToString(),
                DisplayName = ModelIds.GeminiProFlash
            })
            .Append(new()
            {
                Name = EndpointName
                    .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google,
                        ModelIds.GeminiPro).ToString(),
                DisplayName = ModelIds.GeminiPro
            })
            .Where(x => context.SearchString is null || x.DisplayName.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .Take(40)
            .ToDictionary(x => x.Name, x => x.DisplayName);
    }
}