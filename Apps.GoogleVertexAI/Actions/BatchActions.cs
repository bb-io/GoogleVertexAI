using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Batches")]
public class BatchActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
    [Action("Download file result",
    Description = "Reads batch output from GCS and merges output into original file.")]
    public async Task<BatchFileResponse> DownloadXliffFromBatch(
    [ActionParameter, Display("Batch job name")] string jobName,
    [ActionParameter] GetBatchResultRequest originalXliff)
    {
        var region = TryGetLocationFromJobName(jobName, out var loc) ? loc : ResolveVertexRegion();

        var jobClient = ClientFactory.CreateJobService(
            InvocationContext.AuthenticationCredentialsProviders,
            region);

        var job = jobClient.GetBatchPredictionJob(jobName);

        if (job.State != JobState.Succeeded)
            throw new PluginApplicationException($"Batch job is not ready. Current state: {job.State}");

        var outputPrefix = job.OutputConfig?.GcsDestination?.OutputUriPrefix
            ?? throw new PluginApplicationException("Batch job has no GCS output prefix.");

        if (!outputPrefix.StartsWith("gs://"))
            throw new PluginApplicationException("Unexpected output URI prefix.");
        var noScheme = outputPrefix.Substring("gs://".Length);
        var slash = noScheme.IndexOf('/');
        var bucket = slash >= 0 ? noScheme.Substring(0, slash) : noScheme;
        var prefix = slash >= 0 ? noScheme.Substring(slash + 1) : "";

        var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
        var objects = storage.ListObjects(bucket, prefix)
            .Where(o => o.Name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.Name)
            .ToList();

        if (!objects.Any())
            throw new PluginApplicationException("No JSONL outputs found in batch destination.");

        var translations = new Dictionary<string, string>();
        var usage = new UsageDto();
        var warnings = new List<string>();

        foreach (var obj in objects)
        {
            using var ms = new MemoryStream();
            await storage.DownloadObjectAsync(obj, ms);
            ms.Position = 0;

            using var sr = new StreamReader(ms);
            string? line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var jo = JObject.Parse(line);

                var resp = jo["response"];
                if (resp == null)
                {
                    var status = jo["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status)) warnings.Add(status);
                    continue;
                }

                var usageNode = resp["usageMetadata"];
                if (usageNode != null)
                {
                    usage += new UsageDto(new GenerateContentResponse.Types.UsageMetadata
                    {
                        PromptTokenCount = (int?)usageNode["promptTokenCount"] ?? 0,
                        CandidatesTokenCount = (int?)usageNode["candidatesTokenCount"] ?? 0,
                        TotalTokenCount = (int?)usageNode["totalTokenCount"] ?? 0
                    });
                }

                var text = ExtractTextFromResponse(resp);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var m = Regex.Match(text, "\\{ID:(.*?)\\}(.+)$", RegexOptions.Singleline);
                if (!m.Success) { warnings.Add("Cannot find ID prefix in: " + Truncate(text)); continue; }

                var id = m.Groups[1].Value.Trim();
                var content = m.Groups[2].Value.Trim();
                if (!translations.ContainsKey(id))
                    translations[id] = content;
            }
        }

        var stream = await fileManagementClient.DownloadAsync(originalXliff.OriginalXliff);
        var transformation = await Transformation.Parse(stream, originalXliff.OriginalXliff.Name);

        foreach (var pair in transformation.GetSegments().Where(x => !x.IsIgnorbale && x.IsInitial).Select((segment, index) => new { segment, index }))
        {
            if (translations.TryGetValue(pair.index.ToString(), out var tgt))
                pair.segment.SetTarget(tgt);
        }

        var outFile = await fileManagementClient.UploadAsync(transformation.Serialize().ToStream(), MediaTypes.Xliff, transformation.XliffFileName);

        return new BatchFileResponse { File = outFile, Usage = usage, Warnings = warnings };
    }

    private static string ExtractTextFromResponse(JToken response)
    {
        var parts = response["candidates"]?.First?["content"]?["parts"] as JArray;
        if (parts == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            var t = (string?)p["text"];
            if (!string.IsNullOrEmpty(t)) sb.Append(t);
        }
        return sb.ToString();
    }

    private static bool TryGetLocationFromJobName(string jobName, out string location)
    {
        location = string.Empty;

        if (BatchPredictionJobName.TryParse(jobName, out var rn))
        {
            location = rn.LocationId;
            return !string.IsNullOrWhiteSpace(location);
        }
        var m = Regex.Match(jobName,
            @"^projects/[^/]+/locations/(?<loc>[^/]+)/batchPredictionJobs/[^/]+$",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            location = m.Groups["loc"].Value;
            return true;
        }

        return false;
    }
    private static string Truncate(string s, int max = 120)
        => s.Length <= max ? s : s.Substring(0, max) + "…";
}
