using System.Net;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Factories;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.AIPlatform.V1;
using Google.Cloud.Storage.V1;

namespace Apps.GoogleVertexAI.Clients;

// Vertex batch prediction plumbing shared by the background actions and the MQM background report:
// the JSONL input goes to a regional GCS bucket, the job reads it and writes its output next to it.
public static class VertexBatchJobs
{
    public static async Task<BatchPredictionJob> CreateAsync(
        IEnumerable<AuthenticationCredentialsProvider> credentials,
        string projectId,
        string region,
        Stream jsonl,
        string model)
    {
        jsonl.Position = 0;
        var effectiveRegion = ResolveRegion(region);

        try
        {
            var storage = ClientFactory.CreateStorage(credentials);
            var gcsBucket = await EnsureRegionalBucketAsync(storage, projectId, effectiveRegion);

            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var batchIdShort = Guid.NewGuid().ToString("n")[..8];
            var basePrefix = $"xliff/{date}/{batchIdShort}/";

            var inputObject = $"{basePrefix}input.jsonl";
            await storage.UploadObjectAsync(gcsBucket, inputObject, "application/json", jsonl);

            var inputUri = $"gs://{gcsBucket}/{inputObject}";
            var outputPrefix = $"gs://{gcsBucket}/{basePrefix}";

            var normalizedModel = NormalizeModelResourceName(model);

            var jobClient = ClientFactory.CreateJobService(credentials, effectiveRegion);
            var parent = LocationName.FromProjectLocation(projectId, effectiveRegion);

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

            return await jobClient.CreateBatchPredictionJobAsync(parent, job);
        }
        catch (Exception ex) when (ex is not PluginApplicationException)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }

    public static async Task<BatchPredictionJob> GetAsync(IEnumerable<AuthenticationCredentialsProvider> credentials, string jobName)
    {
        var jobClient = ClientFactory.CreateJobService(credentials, LocationOf(jobName));
        return await jobClient.GetBatchPredictionJobAsync(jobName);
    }

    // Batch prediction is regional; the global endpoint is served from us-central1.
    public static string ResolveRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region)) return "us-central1";
        var r = region.Trim().ToLowerInvariant();
        return r == "global" ? "us-central1" : r;
    }

    public static string LocationOf(string jobName)
    {
        if (BatchPredictionJobName.TryParse(jobName, out var resourceName) && !string.IsNullOrWhiteSpace(resourceName.LocationId))
        {
            return resourceName.LocationId;
        }

        var match = Regex.Match(jobName,
            @"^projects/[^/]+/locations/(?<loc>[^/]+)/batchPredictionJobs/[^/]+$",
            RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups["loc"].Value
            : throw new PluginMisconfigurationException(
                "Invalid job name: cannot extract location. Expected 'projects/{p}/locations/{loc}/batchPredictionJobs/{id}'.");
    }

    public static (string Bucket, string Prefix) ParseGcsUri(string uri)
    {
        if (!uri.StartsWith("gs://"))
            throw new PluginApplicationException("Unexpected output URI prefix.");

        var noScheme = uri.Substring("gs://".Length);
        var slash = noScheme.IndexOf('/');
        return slash >= 0
            ? (noScheme.Substring(0, slash), noScheme.Substring(slash + 1))
            : (noScheme, string.Empty);
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

    public static async Task<string> EnsureRegionalBucketAsync(StorageClient storage, string projectId, string region)
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
