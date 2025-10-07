using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Background")]
public class BatchActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
    [Action("Download background file",
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

        var stream = await fileManagementClient.DownloadAsync(originalXliff.OriginalXliff);
        var transformation = await Transformation.Parse(stream, originalXliff.OriginalXliff.Name);
        var backgroundType = transformation.MetaData.FirstOrDefault(x => x.Type == "background-type")?.Value;
        
        var units = transformation.GetUnits();
        var segments = transformation.GetSegments();
        var originalSegments = backgroundType switch
        {
            "translate" => units.SelectMany(x => x.Segments).GetSegmentsForTranslation().ToList(),
            "edit" => units.SelectMany(x => x.Segments).GetSegmentsForEditing().ToList(),
            _ => units.SelectMany(x => x.Segments).Where(x => !x.IsIgnorbale).ToList()
        };

        var translations = new Dictionary<string, string>();
        var usage = new UsageDto();
        var warnings = new List<string>();
        var processedCount = 0;
        var globalIndex = 0;
        
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
                
                var matches = Regex.Matches(text, "\\{ID:(.*?)\\}(.*?)(?=\\{ID:|$)", 
                    RegexOptions.Singleline);
                
                if (matches.Count == 0)
                {
                    try
                    {
                        var arrayResponse = GeminiResponseParser.ParseStringArray(text, InvocationContext.Logger);
                        foreach (var item in arrayResponse.Results)
                        {
                            translations.Add(globalIndex.ToString(), item);
                            globalIndex += 1;
                        }
                    }
                    catch
                    {
                        var bracketMatches = Regex.Matches(text, "\\[ID:(.*?)\\]\\{(.*?)\\}",
                            RegexOptions.Singleline);
                        
                        foreach (Match match in bracketMatches)
                        {
                            if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                            {
                                var id = match.Groups[1].Value.Trim();
                                var content = match.Groups[2].Value.Trim();

                                if (!translations.ContainsKey(id))
                                {
                                    translations[id] = content;
                                    processedCount++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (Match match in matches)
                    {
                        if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                        {
                            var id = match.Groups[1].Value.Trim();
                            var content = match.Groups[2].Value.Trim();

                            if (!translations.ContainsKey(id))
                            {
                                translations[id] = content;
                                processedCount++;
                            }
                        }
                    }
                }
            }
        }
        
        var totalSegmentsCount = segments.Count();
        
        var updatedCount = 0;
        foreach (var pair in originalSegments.Select((segment, index) => new { segment, index }))
        {
            if (translations.TryGetValue(pair.index.ToString(), out var tgt))
            {                
                if (backgroundType == "translate")
                {
                    pair.segment.SetTarget(tgt);
                    pair.segment.State = SegmentState.Translated;
                    updatedCount++;
                }
                else if (backgroundType == "edit")
                {
                    if (pair.segment.GetTarget() != tgt)
                    {
                        pair.segment.SetTarget(tgt);
                        updatedCount++;
                    }
                    pair.segment.State = SegmentState.Reviewed;
                }
                else
                {
                    pair.segment.SetTarget(tgt);
                    updatedCount++;
                }
            }                
        }

        var outFile = await fileManagementClient.UploadAsync(transformation.Serialize().ToStream(), MediaTypes.Xliff, transformation.XliffFileName);
        return new BatchFileResponse { 
            File = outFile, 
            Usage = usage, 
            Warnings = warnings,
            TotalSegmentsCount = totalSegmentsCount,
            ProcessedSegmentsCount = processedCount,
            UpdatedSegmentsCount = updatedCount
        };
    }

    [Action("Get background result",
    Description = "Reads batch output from GCS and returns the combined output.")]
    public async Task<BatchResult> GetBackgroundResult(
    [ActionParameter, Display("Batch job name")] string jobName)
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
        var result = string.Empty;

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
                result += text;                
            }
        }

        return new BatchResult { Result = result, Usage = usage, Warnings = warnings };
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
}
