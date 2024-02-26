using Apps.GoogleVertexAI.Api;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Parameters.Gemini;
using Apps.GoogleVertexAI.Models.Requests.Gemini;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Models.Response.Gemini;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using RestSharp;

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

        var modelId = ModelIds.GeminiPro;
        InlineData? inlineData = null;
        IEnumerable<SafetySetting>? safetySettings = null;

        if (input.Image != null)
        {
            await using var imageStream = await _fileManagementClient.DownloadAsync(input.Image);
            var imageBytes = await imageStream.GetByteData();
            inlineData = new(input.Image.ContentType, Convert.ToBase64String(imageBytes));
            modelId = ModelIds.GeminiProVision;
        }

        if (input.Video != null)
        {
            await using var videoStream = await _fileManagementClient.DownloadAsync(input.Video);
            var videoBytes = await videoStream.GetByteData();
            inlineData = new(input.Video.ContentType, Convert.ToBase64String(videoBytes));
            modelId = ModelIds.GeminiProVision;
        }

        if (input.SafetyCategories != null && input.SafetyCategoryThresholds != null)
            safetySettings = input.SafetyCategories
                .Take(Math.Min(input.SafetyCategories.Count(), input.SafetyCategoryThresholds.Count()))
                .Zip(input.SafetyCategoryThresholds, (category, threshold) => new SafetySetting(category, threshold));
        
        var requestBody = new GeminiParameters(new PromptData[] { new(input.Prompt), new(inlineData) },
            new(input.MaxOutputTokens, input.Temperature, input.TopP, input.TopK), safetySettings);
        
        var request =
            new VertexAiRequest(string.Format(Endpoints.GeminiGenerateContent, modelId), Method.Post)
                .WithJsonBody(requestBody);

        var response = await Client.ExecuteWithErrorHandling<IEnumerable<GenerateTextResponse>>(request);
        var generatedText = string.Join("", response
            .Select(generation => generation.Candidates.First())
            .Select(candidate => candidate.Content.Parts.First().Text))
            .Trim();
        
        return new() { GeneratedText = generatedText };
    }
}