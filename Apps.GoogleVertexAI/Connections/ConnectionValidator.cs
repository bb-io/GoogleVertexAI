﻿using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
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
            var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(authenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value);
            if (serviceConfig == null) throw new Exception("The service config string was not properly formatted");
            var projectId = serviceConfig.ProjectId;

            var endpointService = new EndpointServiceClientBuilder()
            {
                JsonCredentials = authenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value,
                Endpoint = Urls.ApiUrl
            }.Build();

            var res = endpointService.ListEndpointsAsync($"projects/{projectId}/locations/{Urls.Location}");           
            
            return new()
            {
                IsValid = true
            };
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