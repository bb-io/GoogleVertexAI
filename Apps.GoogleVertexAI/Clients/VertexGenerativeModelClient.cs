using System.Text;
using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Api.Gax;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Clients;

public sealed class VertexGenerativeModelClient(
    PredictionServiceClient client,
    string serviceAccountJson,
    string projectId,
    string region,
    Blackbird.Applications.Sdk.Common.Invocation.Logger? logger)
    : GenerativeModelClientBase, IGenerativeModelClient
{
    private readonly PredictionServiceClient _client = client;

    public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
    {
        var isUnprefixed = region.Equals("global", StringComparison.OrdinalIgnoreCase)
                           || region.Equals("us-central1", StringComparison.OrdinalIgnoreCase);

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
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = EndpointName
            .FromProjectLocationPublisherModel(projectId, region, PublisherIds.Google, modelId)
            .ToString();

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

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? 0.9f,
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? 3,
                MaxOutputTokens = input.MaxOutputTokens ?? ModelTokenService.GetMaxTokensForModel(modelId),
            },
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

        try
        {
            using var response = ErrorHandler.ExecuteWithErrorHandling(() => _client.StreamGenerateContent(generateContentRequest));
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();
            var usage = new UsageDto();
            await foreach (var responseItem in responseStream.WithCancellation(cancellationToken))
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

            return (generatedText.ToString(), usage);
        }
        catch (Grpc.Core.RpcException rpcException) when (rpcException.Message.Contains("Error reading next message. HttpIOException"))
        {
            logger?.LogError($"[GoogleGemini] Error reading next message: {rpcException.Message}", []);
            throw new PluginApplicationException(
                "The request to the Gemini API failed, most likely due to message size limits. Try to reduce the batch size (by default it is 1500) or add retry policy. " +
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
}
