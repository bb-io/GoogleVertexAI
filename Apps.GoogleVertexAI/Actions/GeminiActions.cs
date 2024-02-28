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

namespace Apps.GoogleVertexAI.Actions;

[ActionList]
public class GeminiActions : VertexAiInvocable
{
    private readonly IFileManagementClient _fileManagementClient;
    
    public GeminiActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) 
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
        if (input.Image != null && input.Video != null)
            throw new Exception("Please include either an image or a video, but not both.");
        
        var prompt = input.Prompt;
        var modelId = ModelIds.GeminiPro;
        Part? inlineDataPart = null;
        var safetySettings = Enumerable.Empty<SafetySetting>();

        if (input.IsBlackbirdPrompt != null && input.IsBlackbirdPrompt == true)
            prompt = input.Prompt.FromBlackbirdPrompt();

        if (input.Image != null)
        {
            await using var imageStream = await _fileManagementClient.DownloadAsync(input.Image);
            var imageBytes = await imageStream.GetByteData();
            modelId = ModelIds.GeminiProVision;
            inlineDataPart = new Part
            {
                InlineData = new Blob { Data = ByteString.CopyFrom(imageBytes), MimeType = input.Image.ContentType }
            };
        }
        
        if (input.Video != null)
        {
            await using var videoStream = await _fileManagementClient.DownloadAsync(input.Video);
            var videoBytes = await videoStream.GetByteData();
            modelId = ModelIds.GeminiProVision;
            inlineDataPart = new Part
            {
                InlineData = new Blob { Data = ByteString.CopyFrom(videoBytes), MimeType = input.Video.ContentType }
            };
        }

        if (input.SafetyCategories != null && input.SafetyCategoryThresholds != null)
            safetySettings = input.SafetyCategories
                .Take(Math.Min(input.SafetyCategories.Count(), input.SafetyCategoryThresholds.Count()))
                .Zip(input.SafetyCategoryThresholds,
                    (category, threshold) => new SafetySetting
                    {
                        Category = Enum.Parse<HarmCategory>(category),
                        Threshold = Enum.Parse<SafetySetting.Types.HarmBlockThreshold>(threshold)
                    });
        
        var endpoint = EndpointName
            .FromProjectLocationPublisherModel(ProjectId, Urls.Location, PublisherIds.Google, modelId)
            .ToString();
            
        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };
        
        if (inlineDataPart != null)
            content.Parts.Add(inlineDataPart);

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            GenerationConfig = new GenerationConfig
            {
                Temperature = input.Temperature ?? (modelId == ModelIds.GeminiPro ? 0.9f : 0.4f),
                TopP = input.TopP ?? 1.0f,
                TopK = input.TopK ?? (modelId == ModelIds.GeminiPro ? 3 : 32),
                MaxOutputTokens = input.MaxOutputTokens ?? (modelId == ModelIds.GeminiPro ? 8192 : 2048)
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
                generatedText.Append(responseItem.Candidates[0].Content.Parts[0].Text);
            }

            return new() { GeneratedText = generatedText.ToString() };
        }
        catch (Exception exception)
        {
            throw new Exception(exception.Message);
        }
    }
}