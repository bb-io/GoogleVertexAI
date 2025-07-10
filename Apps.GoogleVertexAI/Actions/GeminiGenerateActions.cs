using System.Text;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Apps.GoogleVertexAI.Models.Requests;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Generation")]
public class GeminiGenerateActions : VertexAiInvocable
{
    private readonly IFileManagementClient _fileManagementClient;

    public GeminiGenerateActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Generate text", Description = "Generate text using Gemini. Text generation based on a " +
                                                       "single prompt is executed with the chosen" +
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

        var region = InvocationContext.AuthenticationCredentialsProviders
        .First(p => p.KeyName == CredNames.Region)
        .Value;

        var endpoint = input.AIModel ?? EndpointName
            .FromProjectLocationPublisherModel(ProjectId, region, PublisherIds.Google, input.AIModel)
            .ToString();

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = input.IsBlackbirdPrompt == true ? FromBlackbirdPrompt(input.Prompt) : input.Prompt } }
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

    [Action("Generate text from file", Description = $"Generate text using Gemini. The input can take an additional file. " +
                                                   "Generation will be performed with the chosen" +
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
                new Part { Text = input.IsBlackbirdPrompt == true ? FromBlackbirdPrompt(input.Prompt) : input.Prompt }, 
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

        var endpoint = input.AIModel ?? EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Region, PublisherIds.Google, input.AIModel)
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