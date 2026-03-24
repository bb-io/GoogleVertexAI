using Apps.GoogleVertexAI.Clients.Abstractions;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;
using RestSharp;

namespace Apps.GoogleVertexAI.Clients;

public sealed class GeminiRestGenerativeModelClient(IGeminiApiClient geminiApiClient)
    : GenerativeModelClientBase, IGenerativeModelClient
{
    public async Task ValidateConnectionAsync(CancellationToken cancellationToken)
    {
        var request = geminiApiClient.CreateRequest("v1beta/models?pageSize=1", Method.Get);
        await geminiApiClient.ExecuteAsync(request, cancellationToken);
    }

    public async Task<(string Result, UsageDto Usage)> GenerateTextAsync(
        PromptRequest input,
        string modelId,
        string prompt,
        string? systemPrompt = null,
        IEnumerable<Part>? files = null,
        CancellationToken cancellationToken = default)
    {
        if (files is not null)
        {
            throw new PluginApplicationException("This action requires a service account connection when files are attached.");
        }

        try
        {
            var content = systemPrompt is null
                ? prompt
                : $"{systemPrompt}\n\n{prompt}";

            var request = geminiApiClient.CreateRequest($"v1beta/models/{modelId}:generateContent", Method.Post);
            request.AddJsonBody(new GeminiGenerateContentRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Parts =
                        [
                            new GeminiPart { Text = content }
                        ]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = input.Temperature ?? 0.9f,
                    TopP = input.TopP ?? 1.0f,
                    TopK = input.TopK ?? 3,
                    MaxOutputTokens = input.MaxOutputTokens ?? ModelTokenService.GetMaxTokensForModel(modelId)
                },
                SafetySettings = BuildSafetySettings(input)
            });

            var response = await geminiApiClient.ExecuteAsync<GeminiGenerateContentResponse>(request, cancellationToken);
            return (ExtractText(response), ExtractUsage(response));
        }
        catch (Exception exception) when (exception is not PluginApplicationException)
        {
            throw new PluginApplicationException($"Error: {exception.Message}");
        }
    }
}
