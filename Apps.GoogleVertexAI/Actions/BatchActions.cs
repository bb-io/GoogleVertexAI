using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json.Linq;
using System.Text;
using Blackbird.Ai.Background.Models;
using Blackbird.Ai.Llm.Models;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Background")]
public class BatchActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiContentInvocable(invocationContext, fileManagementClient)
{
    [Action("Download background file",
        Description = "Applies the results of a finished background job to its transformation file.")]
    public async Task<BatchFileResponse> DownloadXliffFromBatch(
        [ActionParameter, Display("Batch job name", Description = "The batch job the transformation file was started with. Optional: the transformation file carries it.")] string? jobName,
        [ActionParameter] GetBatchResultRequest originalXliff)
    {
        var batchClient = CreateBatchClient(null, new PromptRequest());

        await using var file = await FileManagementClient.DownloadAsync(originalXliff.OriginalXliff);

        var result = await AiAdapters.Mapped(() => CreateBackgroundProcessor(batchClient).CompleteAsync(new CompleteBackgroundOptions
        {
            File = file,
            FileName = originalXliff.OriginalXliff.Name,
            MediaType = originalXliff.OriginalXliff.ContentType,
            JobId = string.IsNullOrWhiteSpace(jobName) ? null : jobName.Trim(),
            OutputFileHandling = originalXliff.OutputFileHandling
        }));

        var content = result.Content;

        return new BatchFileResponse
        {
            File = await UploadAsync(result.File),
            Usage = ToBackgroundUsageDto(content.Usage, batchClient),
            Warnings = content.Warnings.ToList(),
            TotalSegmentsCount = content.TotalSegments,
            ProcessedSegmentsCount = content.ProcessableSegments - content.UnresolvedSegments,
            UpdatedSegmentsCount = content.UpdatedSegments,
            UnresolvedSegmentsCount = content.UnresolvedSegments,
            BatchStatus = (result.Job?.State ?? BatchJobState.Completed).ToString().ToLowerInvariant()
        };
    }

    [Action("Get background result",
        Description = "Reads batch output from GCS and returns the combined output.")]
    public async Task<BatchResult> GetBackgroundResult([ActionParameter, Display("Batch job name")] string jobName)
    {
        var job = await VertexBatchJobs.GetAsync(Creds, jobName);

        if (job.State != JobState.Succeeded)
            throw new PluginApplicationException($"Batch job is not ready. Current state: {job.State}");

        var outputPrefix = job.OutputConfig?.GcsDestination?.OutputUriPrefix
            ?? throw new PluginApplicationException("Batch job has no GCS output prefix.");

        var (bucket, prefix) = VertexBatchJobs.ParseGcsUri(outputPrefix);

        var storage = ClientFactory.CreateStorage(InvocationContext.AuthenticationCredentialsProviders);
        var objects = storage.ListObjects(bucket, prefix)
            .Where(o => o.Name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.Name)
            .ToList();

        if (!objects.Any())
            throw new PluginApplicationException("No JSONL outputs found in batch destination.");

        var usage = new UsageDto();
        var warnings = new List<string>();
        var resultBuilder = new StringBuilder();

        foreach (var obj in objects)
        {
            using var ms = new MemoryStream();
            await storage.DownloadObjectAsync(obj, ms);
            ms.Position = 0;

            using var sr = new StreamReader(ms);
            while (await sr.ReadLineAsync() is { } line)
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
                if (!string.IsNullOrWhiteSpace(text))
                    resultBuilder.AppendLine(text);
            }
        }

        return new BatchResult
        {
            Result = resultBuilder.ToString().TrimEnd(), 
            Usage = usage, 
            Warnings = warnings
        };
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
}
