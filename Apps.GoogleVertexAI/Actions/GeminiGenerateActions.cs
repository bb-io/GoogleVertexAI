using System.Text;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Extensions;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests.Gemini;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList]
public class GeminiGenerateActions : VertexAiInvocable
{
    private readonly IFileManagementClient _fileManagementClient;

    public GeminiGenerateActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Generate text with Gemini", Description = "Generate text using Gemini. Text generation based on a " +
                                                       "single prompt is executed with the " +
                                                         ModelIds.GeminiPro +
                                                       " model.")]
    public async Task<GeneratedTextResponse> GenerateText([ActionParameter] GenerateTextRequest input)
    {
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

        var endpoint = input.ModelEndpoint ?? EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google, ModelIds.GeminiPro)
            .ToString();

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = input.IsBlackbirdPrompt == true ? input.Prompt.FromBlackbirdPrompt() : input.Prompt } }
        };

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
            SafetySettings = { safetySettings }
        };
        generateContentRequest.Contents.Add(content);

        try
        {
            using var response = await Utils.ErrorHandler.ExecuteWithErrorHandlingAsync(async () => Client.StreamGenerateContent(generateContentRequest));
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();

            await foreach (var responseItem in responseStream)
            {
                generatedText.Append(responseItem.Candidates[0].Content?.Parts[0].Text ?? string.Empty);
            }

            return new() { GeneratedText = generatedText.ToString() };
        }
        catch (Exception exception)
        {
            throw new PluginApplicationException(exception.Message);
        }
    }
    [Action("Generate text from file with Gemini", Description = $"Generate text using Gemini. " +
                                                   "Generation will be performed with the " + 
                                                   ModelIds.GeminiProFlash +
                                                   " model.")]
    public async Task<GeneratedTextResponse> GenerateTextFromFile([ActionParameter] GenerateTextFromFileRequest input)
    {
        await using var fileStream = await _fileManagementClient.DownloadAsync(input.File);
        var fileBytes = await fileStream.GetByteData();

        var content = new Content
        {
            Role = "USER",
            Parts = 
            { 
                new Part { Text = input.IsBlackbirdPrompt == true ? input.Prompt.FromBlackbirdPrompt() : input.Prompt }, 
                new Part{ InlineData = new Blob { Data = ByteString.CopyFrom(fileBytes), MimeType = input.File.ContentType }}
            }
        };

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

        var endpoint = input.ModelEndpoint ?? EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google, ModelIds.GeminiProFlash)
            .ToString();


        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? 0.4f,
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? 32,
                MaxOutputTokens = input.MaxOutputTokens ?? 2048
            },
            SafetySettings = { safetySettings }
        };
        generateContentRequest.Contents.Add(content);

        try
        {
            using var response = await Utils.ErrorHandler.ExecuteWithErrorHandlingAsync(async () => Client.StreamGenerateContent(generateContentRequest));
            var responseStream = response.GetResponseStream();

            var generatedText = new StringBuilder();

            await foreach (var responseItem in responseStream)
            {
                generatedText.Append(responseItem.Candidates[0].Content?.Parts[0].Text ?? string.Empty);
            }

            return new() { GeneratedText = generatedText.ToString() };
        }
        catch (Exception exception)
        {
            throw new PluginApplicationException(exception.Message);
        }
    }
}