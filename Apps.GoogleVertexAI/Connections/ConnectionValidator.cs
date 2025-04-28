using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Connections;

public class ConnectionValidator : IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        CancellationToken cancellationToken)
    {
        try
        {
            var svc = authenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value;
            var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(authenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value);
            if (serviceConfig == null) throw new PluginApplicationException("The service config string was not properly formatted");
            var projectId = serviceConfig.ProjectId;

            var region = authenticationCredentialsProviders.Get(CredNames.Region).Value;

            var apiUrl = $"https://{region}-aiplatform.googleapis.com";
            var client = new EndpointServiceClientBuilder { JsonCredentials = svc, Endpoint = apiUrl }.Build();

            var list = client.ListEndpointsAsync($"projects/{projectId}/locations/{region}");
            await foreach (var ep in list)
            {

            }
            return new ConnectionValidationResponse { IsValid = true };

            //var endpointService = new EndpointServiceClientBuilder()
            //{
            //    JsonCredentials = authenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value,
            //    Endpoint = Urls.ApiUrl
            //}.Build();

            //var res = endpointService.ListEndpointsAsync($"projects/{projectId}/locations/{Urls.Location}");
            //var result = new List<Endpoint>();
            //await foreach (var endpoint in res)
            //{
            //    result.Add(endpoint);
            //}

            //return new()
            //{
            //    IsValid = true
            //};
        }
        catch (Exception ex)
        {
            return new()
            {
                IsValid = false,
                Message = ex.Message
            };
        }
    }
}