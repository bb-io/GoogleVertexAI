using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Extensions;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Requests.Gemini;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Glossaries.Utils.Converters;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Xliff.Utils;
using Blackbird.Xliff.Utils.Extensions;
using Blackbird.Xliff.Utils.Models;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using MoreLinq;
using Newtonsoft.Json;
using Apps.GoogleVertexAI.Utils.Xliff;
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
                                                       "single prompt is executed with the gemini-1.0-pro model. " +
                                                       "Optionally, you can specify an image or video, and the " +
                                                       "generation will be performed with the gemini-1.0-pro-vision " +
                                                       "model.")]
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
            using var response = Client.StreamGenerateContent(generateContentRequest);
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

    public async Task<GeneratedTextResponse> GenerateTextFromImage([ActionParameter] GenerateTextFromImageRequest input)
    {
        await using var imageStream = await _fileManagementClient.DownloadAsync(input.Image);
        var imageBytes = await imageStream.GetByteData();

        var content = new Content
        {
            Role = "USER",
            Parts = 
            { 
                new Part { Text = input.IsBlackbirdPrompt == true ? input.Prompt.FromBlackbirdPrompt() : input.Prompt }, 
                new Part{ InlineData = new Blob { Data = ByteString.CopyFrom(imageBytes), MimeType = input.Image.ContentType }}
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
            using var response = Client.StreamGenerateContent(generateContentRequest);
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
    public async Task<GeneratedTextResponse> GenerateTextFromVideo([ActionParameter] GenerateTextFromVideoRequest input)
    {

        await using var videoStream = await _fileManagementClient.DownloadAsync(input.Video);
        var videoBytes = await videoStream.GetByteData();

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

        var content = new Content
        {
            Role = "USER",
            Parts = 
            { 
                new Part { Text = input.IsBlackbirdPrompt == true ? input.Prompt.FromBlackbirdPrompt() : input.Prompt}, 
                new Part{ InlineData = new Blob { Data = ByteString.CopyFrom(videoBytes), MimeType = input.Video.ContentType }}
            }
        };

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
            using var response = Client.StreamGenerateContent(generateContentRequest);
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