using System.Text;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;
using Grpc.Core;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;

namespace Apps.GoogleVertexAI.Clients;

public sealed class VertexGenerativeModelClient(
    PredictionServiceClient client,
    string serviceAccountJson,
    string projectId,
    string region,
    Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    : GenerativeModelClientBase, IGenerativeModelClient
{
    private static readonly AsyncRetryPolicy RateLimitPolicy = Policy
        .Handle<RpcException>(exception => exception.StatusCode == StatusCode.ResourceExhausted)
        .WaitAndRetryAsync(RateLimitBackoff.RetryCount, retryAttempt => RateLimitBackoff.GetDelay(retryAttempt));

    public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
    {
        var isUnprefixed = region.Equals("global", StringComparison.OrdinalIgnoreCase);

        var apiUrl = isUnprefixed
            ? "https://aiplatform.googleapis.com"
            : $"https://{region}-aiplatform.googleapis.com";

        var endpointClient = new EndpointServiceClientBuilder
        {
            JsonCredentials = serviceAccountJson,
            Endpoint = apiUrl
        }.Build();

        if (isUnprefixed)
        {
            return;
        }

        await foreach (var endpoint in endpointClient.ListEndpointsAsync(
                           $"projects/{projectId}/locations/{region}").WithCancellation(cancellationToken))
        {
            break;
        }
    }

    public async Task<(string Result, UsageDto Usage)> GenerateTextAsync(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        object? schema = null,
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default)
    {
        var generated = await GenerateAsync(input, modelId, prompt, systemPrompt, schema, null, files, cancellationToken);
        return (generated.Text, generated.Usage);
    }

    public async Task<GeneratedTextDto> GenerateAsync(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        object? schema = null,
        object? responseJsonSchema = null,
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default)
    {
        var generateContentRequest = BuildRequest(
            input,
            BuildModelResourceName(projectId, region, modelId),
            modelId,
            prompt,
            systemPrompt,
            schema ?? responseJsonSchema,
            files);

        try
        {
            // A throttled stream fails before it yields anything useful, so the whole call is repeated.
            return await RateLimitPolicy.ExecuteAsync(
                token => StreamAsync(generateContentRequest, token),
                cancellationToken);
        }
        catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.ResourceExhausted)
        {
            throw new PluginApplicationException(
                "The Vertex AI rate limit was exceeded after multiple retry attempts. Please try again later.");
        }
        catch (Grpc.Core.RpcException rpcException) when (rpcException.Message.Contains("Error reading next message. HttpIOException"))
        {
            logger?.LogError($"[GoogleGemini] Error reading next message: {rpcException.Message}", []);
            throw new PluginApplicationException(
                "The request to the Gemini API failed, most likely due to message size limits. Try to reduce the bucket size or add retry policy." +
                "If the issue persists, please contact support with the error details.");
        }
        catch (NullReferenceException nullEx)
        {
            logger?.LogError($"[GoogleGemini] Null reference error while processing response: {nullEx.Message}", []);
            throw new PluginApplicationException(
                "An error occurred while processing the response from Gemini API. Some expected data was missing in the response. " +
                "Please try again or contact support if the issue persists.");
        }
        catch (Exception exception)
        {
            throw new PluginApplicationException($"Error: {exception.Message}");
        }
    }

    // Shared with VertexBatchLlmClient, so a batch line carries exactly the request a direct call sends.
    internal static GenerateContentRequest BuildRequest(
        PromptRequest input,
        string model,
        string modelId,
        string prompt,
        string? systemPrompt,
        object? schema,
        IEnumerable<Part>? files = null)
    {
        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        if (files is not null)
        {
            foreach (var file in files)
            {
                content.Parts.Add(file);
            }
        }
        
        var config = new GenerationConfig
        {
            Temperature = input.Temperature ?? 0.9f,
            TopP = input.TopP ?? 1.0f,
            TopK = input.TopK ?? 3,
            MaxOutputTokens = input.MaxOutputTokens ?? ModelTokenService.GetMaxTokensForModel(modelId),
        };

        // Vertex takes both the OpenAPI-style schemas and standard JSON Schema through responseJsonSchema.
        if (schema is not null)
        {
            string jsonSchemaString = JsonConvert.SerializeObject(schema);
            config.ResponseMimeType = "application/json";
            config.ResponseJsonSchema = Google.Protobuf.WellKnownTypes.Value.Parser.ParseJson(jsonSchemaString);
        }

        var generateContentRequest = new GenerateContentRequest
        {
            Model = model,
            GenerationConfig = config,
            SafetySettings = { BuildVertexSafetySettings(input) },
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

        return generateContentRequest;
    }

    // Shared with VertexBatchLlmClient: a streamed answer arrives in chunks, a batch result line in one.
    internal static GeneratedTextDto ToGeneratedText(IEnumerable<GenerateContentResponse?> responses)
    {
        var generatedText = new StringBuilder();
        var usage = new UsageDto();
        string? finishReason = null;
        foreach (var responseItem in responses)
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
            if (candidate is not null && candidate.FinishReason != Candidate.Types.FinishReason.Unspecified)
            {
                finishReason = candidate.FinishReason == Candidate.Types.FinishReason.MaxTokens
                    ? MaxTokensFinishReason
                    : candidate.FinishReason.ToString();
            }

            if (candidate?.Content?.Parts is null || candidate.Content.Parts.Count == 0)
            {
                continue;
            }

            var currentText = candidate.Content.Parts[0]?.Text;
            if (!string.IsNullOrEmpty(currentText))
            {
                generatedText.Append(currentText);
            }
        }

        return new GeneratedTextDto(generatedText.ToString(), usage, finishReason);
    }

    private async Task<GeneratedTextDto> StreamAsync(
        GenerateContentRequest generateContentRequest,
        CancellationToken cancellationToken)
    {
        using var response = client.StreamGenerateContent(generateContentRequest);
        var responseStream = response.GetResponseStream();

        var chunks = new List<GenerateContentResponse?>();
        await foreach (var responseItem in responseStream.WithCancellation(cancellationToken))
        {
            chunks.Add(responseItem);
        }

        return ToGeneratedText(chunks);
    }

    internal static string BuildModelResourceName(string projectId, string region, string modelId)
    {
        var normalizedModelId = modelId.Trim();

        if (EndpointName.TryParse(normalizedModelId, out var resourceName))
        {
            return resourceName.ToString();
        }

        var isCustomEndpoint = normalizedModelId.Length > 0 && normalizedModelId.All(char.IsAsciiDigit);

        return isCustomEndpoint
            ? EndpointName.FromProjectLocationEndpoint(projectId, region, normalizedModelId).ToString()
            : EndpointName
                .FromProjectLocationPublisherModel(projectId, region, PublisherIds.Google, normalizedModelId)
                .ToString();
    }
}
