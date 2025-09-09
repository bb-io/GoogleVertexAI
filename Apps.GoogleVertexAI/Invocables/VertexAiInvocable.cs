using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.AIPlatform.V1;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace Apps.GoogleVertexAI.Invocables;

public class VertexAiInvocable : BaseInvocable
{
    protected readonly PredictionServiceClient Client;
    protected readonly JobServiceClient JobClient;
    protected readonly StorageClient Storage;
    protected readonly string ProjectId;
    protected readonly string Region;

    protected AuthenticationCredentialsProvider[] Creds =>
        InvocationContext.AuthenticationCredentialsProviders.ToArray();

    protected VertexAiInvocable(InvocationContext invocationContext) : base(invocationContext)
    {
        Region = invocationContext.AuthenticationCredentialsProviders.Get(CredNames.Region).Value;
        Client = ClientFactory.Create(invocationContext.AuthenticationCredentialsProviders,Region);
        JobClient = ClientFactory.CreateJobService(invocationContext.AuthenticationCredentialsProviders, Region);
        Storage = ClientFactory.CreateStorage(invocationContext.AuthenticationCredentialsProviders);

        var serviceConfig = JsonConvert.DeserializeObject<ServiceAccountConfig>(invocationContext.AuthenticationCredentialsProviders.Get(CredNames.ServiceAccountConfString).Value);
        if (serviceConfig == null) throw new Exception("The service config string was not properly formatted");
        ProjectId = serviceConfig.ProjectId;
    }

    protected async Task<string> IdentifySourceLanguage(PromptRequest promptRequest, string modelId, string content)
    {
        var systemPrompt = "You are a linguist. Identify the language of the following text. Your response should be in the BCP 47 (language) or (language-country). You respond with the language only, not other text is required.";

        var snippet = content.Length > 200 ? content.Substring(0, 300) : content;
        var userPrompt = snippet + ". The BCP 47 language code: ";

        var (result, usage) = await ExecuteGeminiPrompt(promptRequest, modelId, userPrompt, systemPrompt);

        return result;
    }

    protected async Task<(string result, UsageDto usage)> ExecuteGeminiPrompt(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        IEnumerable<Part>? files = null)
    {
        var endpoint = EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Region, PublisherIds.Google, modelId)
            .ToString();

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        if (files is not null )
        {
            foreach (var file in files)
            {
                content.Parts.Add(file);
            }
        }

        var safetySettings = input is { SafetyCategories: not null, SafetyCategoryThresholds: not null }
            ? input.SafetyCategories
                .Take(Math.Min(input.SafetyCategories.Count(), input.SafetyCategoryThresholds.Count()))
                .Zip(input.SafetyCategoryThresholds,
                    (category, threshold) => new SafetySetting
                    {
                        Category = Enum.Parse<HarmCategory>(category),
                        Threshold = Enum.Parse<SafetySetting.Types.HarmBlockThreshold>(threshold)
                    })
            : Enumerable.Empty<SafetySetting>();

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? 0.9f,
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? 3,
                MaxOutputTokens = input.MaxOutputTokens ?? 8192
            },
            SafetySettings = { safetySettings },
            SystemInstruction = systemPrompt is null
                ? null
                : new()
                {
                    Parts =
                    {
                        new Part { Text = systemPrompt },
                    }
                }
        };
        generateContentRequest.Contents.Add(content);
        var lastMessage = string.Empty;

        try
        {
            using var response = ErrorHandler.ExecuteWithErrorHandling(() => Client.StreamGenerateContent(generateContentRequest));
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();
            var usage = new UsageDto();
            await foreach (var responseItem in responseStream)
            {
                if (responseItem?.UsageMetadata is not null)
                {
                    usage += new UsageDto(responseItem.UsageMetadata);
                }

                if (responseItem?.Candidates is null || responseItem.Candidates.Count == 0)
                {
                    continue;
                }

                var candidate = responseItem.Candidates[0];
                if (candidate?.Content is null)
                {
                    continue;
                }

                if (candidate.Content.Parts is null || candidate.Content.Parts.Count == 0)
                {
                    continue;
                }

                var part = candidate.Content.Parts[0];
                var currentText = part?.Text;

                if (!string.IsNullOrEmpty(currentText))
                {
                    lastMessage = currentText;
                    generatedText.Append(currentText);
                }
            }

            return (generatedText.ToString(), usage);
        }
        catch (Grpc.Core.RpcException rpcException) when (rpcException.Message.Contains("Error reading next message. HttpIOException"))
        {
            InvocationContext.Logger?.LogError($"[GoogleGemini] Error reading next message: {rpcException.Message}", []);
            throw new PluginApplicationException(
                "The request to the Gemini API failed, most likely due to message size limits. Try to reduce the batch size (by default it is 1500) or add retry policy. " +
                "If the issue persists, please contact support with the error details.");
        }
        catch (NullReferenceException nullEx)
        {
            InvocationContext.Logger?.LogError($"[GoogleGemini] Null reference error while processing response: {nullEx.Message}", []);
            throw new PluginApplicationException(
                "An error occurred while processing the response from Gemini API. Some expected data was missing in the response. " +
                "Please try again or contact support if the issue persists.");
        }
        catch (Exception exception)
        {
            throw new PluginApplicationException($"Error: {exception.Message}");
        }
    }

    protected async Task<BatchPredictionJob> CreateBatchRequest(Stream jsonlMs, string model)
    {
        jsonlMs.Position = 0;
        var effectiveRegion = ResolveVertexRegion();

        try
        {
            var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
            var gcsBucket = await EnsureRegionalBucketAsync(storage, ProjectId, effectiveRegion);

            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var batchIdShort = Guid.NewGuid().ToString("n")[..8];
            var basePrefix = $"xliff/{date}/{batchIdShort}/";

            var inputObject = $"{basePrefix}input.jsonl";
            await storage.UploadObjectAsync(gcsBucket, inputObject, "application/json", jsonlMs);

            var inputUri = $"gs://{gcsBucket}/{inputObject}";
            var outputPrefix = $"gs://{gcsBucket}/{basePrefix}";

            var normalizedModel = NormalizeModelResourceName(model);

            var jobClient = ClientFactory.CreateJobService(InvocationContext.AuthenticationCredentialsProviders, effectiveRegion);
            var parent = LocationName.FromProjectLocation(ProjectId, effectiveRegion);

            var job = new BatchPredictionJob
            {
                DisplayName = $"xliff-{effectiveRegion}-{date}-{batchIdShort}",
                Model = normalizedModel,
                InputConfig = new BatchPredictionJob.Types.InputConfig
                {
                    InstancesFormat = "jsonl",
                    GcsSource = new GcsSource { Uris = { inputUri } }
                },
                OutputConfig = new BatchPredictionJob.Types.OutputConfig
                {
                    PredictionsFormat = "jsonl",
                    GcsDestination = new GcsDestination { OutputUriPrefix = outputPrefix }
                }
            };

            return jobClient.CreateBatchPredictionJob(parent, job);
        }
        catch (Exception ex) {
            throw new PluginApplicationException(ex.Message);
        }        
    }

    protected string ResolveVertexRegion()
    {
        if (string.IsNullOrWhiteSpace(Region)) return "us-central1";
        var r = Region.Trim().ToLowerInvariant();
        return r == "global" ? "us-central1" : r;
    }
    private static string NormalizeModelResourceName(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new PluginMisconfigurationException("Model is required.");

        var m = model.Trim();

        if (m.StartsWith("projects/", StringComparison.OrdinalIgnoreCase)) return m;
        if (m.StartsWith("publishers/", StringComparison.OrdinalIgnoreCase)) return m;

        if (m.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            return $"publishers/google/{m}";

        return $"publishers/google/models/{m}";
    }

    private static async Task<string> EnsureRegionalBucketAsync(StorageClient storage, string projectId, string region)
    {
        var bucketName = $"blackbird-batch-{projectId}-{region}"
         .ToLowerInvariant()
         .Replace("_", "-");

        try
        {
            var existing = await storage.GetBucketAsync(bucketName);
            if (existing == null)
                throw new PluginApplicationException($"Bucket '{bucketName}' could not be retrieved.");

            if (!IsCompatibleLocation(existing.Location, region))
            {
                throw new PluginApplicationException(
                    $"Bucket '{bucketName}' is in '{existing.Location}', but job region is '{region}'. " +
                    $"Use a compatible location (same region or a multi-region that includes it), " +
                    $"or let the action create one automatically.");
            }

            return bucketName;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            var bucket = new Bucket
            {
                Name = bucketName,
                Location = region,
                StorageClass = "STANDARD",
                IamConfiguration = new Bucket.IamConfigurationData
                {
                    UniformBucketLevelAccess = new Bucket.IamConfigurationData.UniformBucketLevelAccessData { Enabled = true }
                }
            };

            try
            {
                await storage.CreateBucketAsync(projectId, bucket);
                return bucketName;
            }
            catch (Google.GoogleApiException createEx) when (createEx.HttpStatusCode == HttpStatusCode.Conflict)
            {
                var alt = $"{bucketName}-{Guid.NewGuid().ToString("n")[..6]}";
                var altBucket = new Bucket
                {
                    Name = alt,
                    Location = region,
                    StorageClass = "STANDARD",
                    IamConfiguration = bucket.IamConfiguration
                };
                await storage.CreateBucketAsync(projectId, altBucket);
                return alt;
            }
        }
    }

    private static bool IsCompatibleLocation(string? bucketLocation, string region)
    {
        if (string.IsNullOrWhiteSpace(bucketLocation)) return false;

        var b = bucketLocation.Trim().ToLowerInvariant();
        var r = region.Trim().ToLowerInvariant();

        if (b == r) return true;

        if (b == "us" && r.StartsWith("us-")) return true;
        if (b == "eu" && (r.StartsWith("europe-") || r.StartsWith("eu-"))) return true;
        if (b == "asia" && r.StartsWith("asia-")) return true;

        return false;
    }
}