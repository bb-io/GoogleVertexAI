using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Cloud.AIPlatform.V1;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Invocables;

public class VertexAiInvocable : BaseInvocable
{
    protected readonly PredictionServiceClient? Client;
    protected readonly JobServiceClient? JobClient;
    protected readonly StorageClient? Storage;
    protected readonly IGeminiApiClient GeminiApiClient;
    protected readonly string ProjectId;
    protected readonly string Region;
    protected readonly string ConnectionType;

    private readonly IGenerativeModelClient _generativeModelClient;

    protected IGenerativeModelClient GenerativeModelClient => _generativeModelClient;

    protected AuthenticationCredentialsProvider[] Creds =>
        InvocationContext.AuthenticationCredentialsProviders.ToArray();

    protected VertexAiInvocable(InvocationContext invocationContext) : base(invocationContext)
    {
        ConnectionType = invocationContext.AuthenticationCredentialsProviders
            .FirstOrDefault(x => x.KeyName == CredNames.ConnectionType)?.Value ?? ConnectionTypes.ServiceAccount;
        Region = invocationContext.AuthenticationCredentialsProviders
            .FirstOrDefault(x => x.KeyName == CredNames.Region)?.Value ?? "global";

        GeminiApiClient = GeminiApiClientFactory.Create(
            invocationContext.AuthenticationCredentialsProviders,
            invocationContext.Logger);
        _generativeModelClient = GenerativeModelClientFactory.Create(
            invocationContext.AuthenticationCredentialsProviders,
            invocationContext.Logger,
            Region);

        if (ConnectionType == ConnectionTypes.ServiceAccount)
        {
            Client = ClientFactory.Create(invocationContext.AuthenticationCredentialsProviders, Region);
            JobClient = ClientFactory.CreateJobService(invocationContext.AuthenticationCredentialsProviders, Region);
            Storage = ClientFactory.CreateStorage(invocationContext.AuthenticationCredentialsProviders);

            var serviceAccountJson = invocationContext.AuthenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value;
            var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(serviceAccountJson);
            if (serviceConfig == null) throw new Exception("The service config string was not properly formatted");
            ProjectId = serviceConfig.ProjectId;
        }
        else
        {
            ProjectId = string.Empty;
        }
    }

    protected async Task<(string result, UsageDto usage)> ExecuteGeminiPrompt(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        object? schema = null,
        IEnumerable<Part>? files = null)
        => await _generativeModelClient.GenerateTextAsync(input, modelId, prompt, systemPrompt, schema, files);

    protected bool IsGeminiApiKeyConnection()
        => ConnectionType == ConnectionTypes.GeminiApiKey;

    protected void EnsureGeminiApiKeyConnection(string capability)
    {
        if (!IsGeminiApiKeyConnection())
        {
            throw new PluginMisconfigurationException($"{capability} requires a Gemini API key connection.");
        }
    }

    protected void EnsureVertexAiConnection()
    {
        if (ConnectionType != ConnectionTypes.ServiceAccount)
        {
            throw new PluginMisconfigurationException("This action requires a service account connection.");
        }
    }

    protected async Task<BatchPredictionJob> CreateBatchRequest(Stream jsonlMs, string model)
    {
        EnsureVertexAiConnection();
        return await VertexBatchJobs.CreateAsync(InvocationContext.AuthenticationCredentialsProviders, ProjectId, Region, jsonlMs, model);
    }

    protected string ResolveVertexRegion() => VertexBatchJobs.ResolveRegion(Region);

    protected static Task<string> EnsureRegionalBucketAsync(StorageClient storage, string projectId, string region) =>
        VertexBatchJobs.EnsureRegionalBucketAsync(storage, projectId, region);
}
