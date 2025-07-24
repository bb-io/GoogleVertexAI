using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Apps.GoogleVertexAI.Models.Requests;

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

        var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, input.AIModel, prompt);

        return new()
        {
            GeneratedText = response,
            Usage = promptUsage,
        };
    }

    [Action("Generate text from files", Description = $"Generate text using Gemini. The input can take additional files. Generation will be performed with the chosen model.")]
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

        return new() {
            GeneratedText = response,
            Usage = promptUsage,
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