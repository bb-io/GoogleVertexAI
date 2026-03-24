using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using RestSharp;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Generation")]
public class GeminiGenerateActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiInvocable(invocationContext)
{
    [Action("Generate text", Description = "Generate text using Gemini. Text generation based on a single prompt is executed with the chosen model.")]
    public async Task<GeneratedTextResponse> GenerateText(
        [ActionParameter] GenerateTextRequest input,
        [ActionParameter] PromptRequest promptRequest)
    {
        var prompt = input.IsBlackbirdPrompt == true
            ? FromBlackbirdPrompt(input.Prompt)
            : input.Prompt;

        var fileSearchStoreNames = input.FileSearchStoreNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (fileSearchStoreNames is { Count: > 0 })
        {
            EnsureGeminiApiKeyConnection("Searching across File Search stores");

            var fileSearchResponse = await ExecuteFileSearchGenerateAsync(
                input.AIModel,
                prompt,
                promptRequest,
                fileSearchStoreNames,
                input.MetadataFilter);

            return new()
            {
                GeneratedText = ExtractText(fileSearchResponse),
                Usage = ExtractUsage(fileSearchResponse),
                RetrievedContexts = ExtractRetrievedContexts(fileSearchResponse)
            };
        }

        var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, input.AIModel, prompt);

        return new()
        {
            GeneratedText = response,
            Usage = promptUsage,
        };
    }

    [Action("Generate text from files", Description = "Generate text using Gemini. The input can take additional files. Generation will be performed with the chosen model.")]
    public async Task<GeneratedTextResponse> GenerateTextFromFile(
        [ActionParameter] GenerateTextFromFileRequest input,
        [ActionParameter] PromptRequest promptRequest)
    {
        var prompt = input.IsBlackbirdPrompt == true
            ? FromBlackbirdPrompt(input.Prompt)
            : input.Prompt;

        var files = new List<Part> { };

        foreach (var file in input.Files)
        {
            if (file is null)
                continue;

            await using var stream = await fileManagementClient.DownloadAsync(file);
            var fileBytes = await stream.GetByteData();
            files.Add(new() { InlineData = new Blob { Data = ByteString.CopyFrom(fileBytes), MimeType = file.ContentType } });
        }

        var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, input.AIModel, prompt, files: files);

        return new()
        {
            GeneratedText = response,
            Usage = promptUsage,
        };
    }

    private async Task<GeminiGenerateContentResponse> ExecuteFileSearchGenerateAsync(
        string modelId,
        string prompt,
        PromptRequest promptRequest,
        IEnumerable<string> fileSearchStoreNames,
        string? metadataFilter)
    {
        try
        {
            var request = GeminiApiClient.CreateRequest($"v1beta/models/{modelId}:generateContent", Method.Post);
            request.AddJsonBody(new GeminiGenerateContentRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Parts =
                        [
                            new GeminiPart { Text = prompt }
                        ]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = promptRequest.Temperature ?? 0.9f,
                    TopP = promptRequest.TopP ?? 1.0f,
                    TopK = promptRequest.TopK ?? 3,
                    MaxOutputTokens = promptRequest.MaxOutputTokens ?? ModelTokenService.GetMaxTokensForModel(modelId)
                },
                SafetySettings = BuildSafetySettings(promptRequest),
                Tools = BuildTools(fileSearchStoreNames, metadataFilter)
            });

            return await GeminiApiClient.ExecuteAsync<GeminiGenerateContentResponse>(request);
        }
        catch (Exception exception) when (exception is not PluginApplicationException)
        {
            throw new PluginApplicationException($"Error: {exception.Message}");
        }
    }

    private static string ExtractText(GeminiGenerateContentResponse response)
    {
        var candidate = response.Candidates?.FirstOrDefault();
        return string.Concat(candidate?.Content?.Parts?
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text) ?? []);
    }

    private static UsageDto ExtractUsage(GeminiGenerateContentResponse response)
    {
        return response.UsageMetadata is null
            ? new UsageDto()
            : new UsageDto(
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount,
                response.UsageMetadata.TotalTokenCount);
    }

    private static List<RetrievedContextDto> ExtractRetrievedContexts(GeminiGenerateContentResponse response)
    {
        return response.Candidates?.FirstOrDefault()?.GroundingMetadata?.GroundingChunks?
            .Select(x => x.RetrievedContext)
            .Where(x => x is not null)
            .Select(x => new RetrievedContextDto
            {
                Title = x!.Title,
                Uri = x.Uri,
                Text = x.Text,
                FileSearchStore = x.FileSearchStore
            })
            .ToList() ?? [];
    }

    private static List<GeminiTool>? BuildTools(IEnumerable<string> fileSearchStoreNames, string? metadataFilter)
    {
        var stores = fileSearchStoreNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stores.Count == 0)
        {
            return null;
        }

        return
        [
            new GeminiTool
            {
                FileSearch = new GeminiFileSearchTool
                {
                    FileSearchStoreNames = stores,
                    MetadataFilter = string.IsNullOrWhiteSpace(metadataFilter) ? null : metadataFilter
                }
            }
        ];
    }

    private static List<GeminiSafetySetting>? BuildSafetySettings(PromptRequest promptRequest)
    {
        if (promptRequest is not { SafetyCategories: not null, SafetyCategoryThresholds: not null })
        {
            return null;
        }

        return promptRequest.SafetyCategories
            .Take(Math.Min(promptRequest.SafetyCategories.Count(), promptRequest.SafetyCategoryThresholds.Count()))
            .Zip(promptRequest.SafetyCategoryThresholds,
                (category, threshold) => new GeminiSafetySetting
                {
                    Category = MapSafetyCategory(category),
                    Threshold = MapSafetyThreshold(threshold)
                })
            .ToList();
    }

    private static string MapSafetyCategory(string category)
    {
        return category switch
        {
            "SexuallyExplicit" => "HARM_CATEGORY_SEXUALLY_EXPLICIT",
            "HateSpeech" => "HARM_CATEGORY_HATE_SPEECH",
            "Harassment" => "HARM_CATEGORY_HARASSMENT",
            "DangerousContent" => "HARM_CATEGORY_DANGEROUS_CONTENT",
            _ => throw new PluginApplicationException($"Unsupported safety category: {category}")
        };
    }

    private static string MapSafetyThreshold(string threshold)
    {
        return threshold switch
        {
            "BlockNone" => "BLOCK_NONE",
            "BlockLowAndAbove" => "BLOCK_LOW_AND_ABOVE",
            "BlockMediumAndAbove" => "BLOCK_MEDIUM_AND_ABOVE",
            "BlockOnlyHigh" => "BLOCK_ONLY_HIGH",
            _ => throw new PluginApplicationException($"Unsupported safety threshold: {threshold}")
        };
    }

    private static string FromBlackbirdPrompt(string inputPrompt)
    {
        var promptSegments = inputPrompt.Split(";;");

        if (promptSegments.Length == 1)
            return promptSegments[0];

        if (promptSegments.Length == 2 || promptSegments.Length == 3)
            return $"{promptSegments[0]}\n\n{promptSegments[1]}";

        throw new("Wrong blackbird prompt format");
    }
}
