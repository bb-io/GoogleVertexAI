using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Ai.Background;
using Blackbird.Ai.Background.Models;
using Blackbird.Ai.Batching.Models;
using Blackbird.Ai.Content.Models;
using Blackbird.Ai.Files;
using Blackbird.Ai.Files.Models;
using Blackbird.Ai.Llm.Abstract;
using Blackbird.Ai.Llm.Models;
using Blackbird.Ai.Text;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;

namespace Apps.GoogleVertexAI.Invocables;

// Shared plumbing for the actions that run on the Blackbird.Ai pipeline.
public class VertexAiContentInvocable(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiInvocable(invocationContext)
{
    private const string LogTag = "[GoogleGemini]";

    protected IFileManagementClient FileManagementClient { get; } = fileManagementClient;

    // Virtual so a test can substitute or wrap the provider client.
    protected virtual ILlmClient CreateLlmClient(string model, PromptRequest settings) =>
        new GeminiLlmClient(GenerativeModelClient, model, settings);

    // Virtual so a test can substitute a scripted batch provider.
    protected virtual IBatchLlmClient CreateBatchClient(string? model, PromptRequest settings)
    {
        EnsureVertexAiConnection();
        return new VertexBatchLlmClient(InvocationContext.AuthenticationCredentialsProviders, ProjectId, Region, model, settings);
    }

    // Download needs no model: the provenance and the buckets come from the work file.
    protected EngineOptions CreateEngineOptions(string? model, int? bucketSize = null) => new()
    {
        Logger = AiAdapters.Logger(InvocationContext),
        LogTag = LogTag,
        ProvenanceTool = model ?? "Google Gemini",
        Bucketing = BucketingOptions.FromInputs(bucketSize, null)
    };

    // Starting a job needs the synchronous client for language detection and the summary; downloading does not.
    protected BackgroundContentProcessor CreateBackgroundProcessor(
        IBatchLlmClient batchClient,
        ILlmClient? client = null,
        string? model = null,
        int? bucketSize = null) =>
        new(batchClient, client, CreateEngineOptions(model, bucketSize));

    protected async Task<StartBatchResponse> ToStartBatchResponseAsync(BackgroundStart started, ILlmClient client)
    {
        var content = started.Content;

        return new StartBatchResponse
        {
            // With nothing to process no batch job is created; the download then returns the file unchanged.
            JobName = started.Job?.Id ?? string.Empty,
            TransformationFile = await UploadAsync(started.WorkFile),
            TotalSegmentsCount = content.TotalSegments,
            SegmentsToProcessCount = content.ProcessableSegments,
            RequestsCount = started.Requests,
            SystemPrompt = content.SystemPrompt ?? string.Empty,
            Usage = ToUsageDto(content.Usage, client)
        };
    }

    // Complete reports the preparation usage recorded at start plus the batch; the batch client keeps
    // Gemini's own usage of the batch lines, thinking tokens included.
    protected static UsageDto ToBackgroundUsageDto(LlmUsage total, IBatchLlmClient batchClient)
    {
        if (batchClient is not VertexBatchLlmClient vertexClient)
        {
            return new UsageDto(total.InputTokens, total.OutputTokens, total.TotalTokens);
        }

        var reported = vertexClient.ReportedUsage;
        var preparationInput = total.InputTokens - reported.InputTokens;
        var preparationOutput = total.OutputTokens - reported.OutputTokens;

        return vertexClient.Usage + new UsageDto(preparationInput, preparationOutput, preparationInput + preparationOutput);
    }

    protected ContentStreamProcessor CreateContentProcessor(ILlmClient client, string? model, int? bucketSize = null) =>
        new(client, CreateEngineOptions(model, bucketSize));

    protected TextProcessor CreateTextProcessor(ILlmClient client, string? model) =>
        new(client, CreateEngineOptions(model));

    protected async Task<Stream?> DownloadOptionalAsync(FileReference? file) =>
        file is null ? null : await FileManagementClient.DownloadAsync(file);

    protected async Task<FileReference> UploadAsync(ContentStream file)
    {
        await using var content = file.Content;
        return await FileManagementClient.UploadAsync(content, file.MediaType, file.FileName);
    }

    // The client keeps Gemini's own usage (its total includes thinking tokens); LlmUsage is the fallback.
    protected static UsageDto ToUsageDto(LlmUsage usage, ILlmClient client) =>
        client is GeminiLlmClient geminiClient
            ? geminiClient.Usage
            : new UsageDto(usage.InputTokens, usage.OutputTokens, usage.TotalTokens);

    // Text actions have one item; when the model never gave a usable answer there is nothing to return.
    protected static void ThrowIfUnresolved(ContentResult content)
    {
        if (content.UnresolvedSegments > 0)
        {
            throw new PluginApplicationException(string.Join(Environment.NewLine, content.Warnings));
        }
    }
}
