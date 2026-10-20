using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Ai.Files.Models;
using Blackbird.Ai.Summary.Models;
using Blackbird.Ai.Text.Models;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Translation")]
public class TranslationActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiContentInvocable(invocationContext, fileManagementClient)
{
    [BlueprintActionDefinition(BlueprintAction.TranslateFile)]
    [Action("Translate", Description = "Translate file content retrieved from a CMS or file storage. The output can be used in compatible actions.")]
    public async Task<FileTranslationResponse> TranslateContent(
        [ActionParameter] TranslateFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the maximum number of source texts to be translated at once. Default value: 250. Buckets are also sized to fit the model's output limit. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateContentProcessor(client, input.AIModel, bucketSize);

        await using var file = await FileManagementClient.DownloadAsync(input.File);
        await using var glossaryFile = await DownloadOptionalAsync(glossary.Glossary);

        var result = await AiAdapters.Mapped(() =>
            processor.TranslateAsync(TranslateOptions(input, file, glossaryFile, prompt)));

        var content = result.Content;

        return new FileTranslationResponse
        {
            File = await UploadAsync(result.File),
            TotalSegmentsCount = content.TotalSegments,
            TotalTranslatable = content.ProcessableSegments,
            TargetsUpdatedCount = content.UpdatedSegments,
            ProcessedBatchesCount = content.BucketsProcessed,
            ErrorMessages = content.Warnings.ToList()
        };
    }

    [Action("Translate in background", Description = "Translate file content retrieved from a CMS or file storage. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<StartBatchResponse> BatchTranslateContent(
        [ActionParameter] TranslateFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary, 
        [ActionParameter] BucketRequest bucketRequest)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateBackgroundProcessor(
            CreateBatchClient(input.AIModel, promptRequest),
            client,
            input.AIModel,
            bucketRequest.BucketSize);

        await using var file = await FileManagementClient.DownloadAsync(input.File);
        await using var glossaryFile = await DownloadOptionalAsync(glossary.Glossary);

        var started = await AiAdapters.Mapped(() =>
            processor.StartTranslateAsync(TranslateOptions(input, file, glossaryFile, prompt)));

        return await ToStartBatchResponseAsync(started, client);
    }

    [BlueprintActionDefinition(BlueprintAction.TranslateText)]
    [Action("Translate text", Description = "Localize the text provided.")]
    public async Task<TextTranslationResponse> LocalizeText(
        [ActionParameter] TranslateTextRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary)
    {
        var client = CreateLlmClient(input.AIModel, promptRequest);
        var processor = CreateTextProcessor(client, input.AIModel);

        await using var glossaryFile = await DownloadOptionalAsync(glossary.Glossary);

        var result = await AiAdapters.Mapped(() => processor.TranslateAsync(new TranslateTextOptions
        {
            Text = input.Text,
            SourceLanguage = input.SourceLanguage,
            TargetLanguage = input.TargetLanguage,
            AdditionalInstructions = prompt,
            GlossaryTbx = glossaryFile
        }));

        ThrowIfUnresolved(result.Content);

        return new()
        {
            TranslatedText = result.Text,
        };
    }
    
    // Shared by "Translate" and "Translate in background", so the two cannot drift apart.
    private static TranslateStreamOptions TranslateOptions(
        TranslateFileRequest input,
        Stream file,
        Stream? glossaryFile,
        string? prompt) => new()
    {
        File = file,
        FileName = input.File.Name,
        MediaType = input.File.ContentType,
        SourceLanguage = input.SourceLanguage,
        TargetLanguage = input.TargetLanguage,
        OutputFileHandling = input.OutputFileHandling,
        GlossaryTbx = glossaryFile,
        AdditionalInstructions = prompt,
        Summary = SummaryOptions.FromToggle(input.GenerateDocumentSummary)
    };
}
