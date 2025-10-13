using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Glossaries.Utils.Dtos;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Apps.GoogleVertexAI.Utils;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Reporting")]
public class ReportingActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : VertexAiInvocable(invocationContext)
{
    private async Task<(string userPrompt, string systemPrompt)> CreatePrompts(GetTranslationIssuesRequest input, string? customSystemPrompt, GlossaryRequest glossary, string? additionalPrompt, Transformation content)
    {
        var sourceLanguage = input.SourceLanguage ?? content.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? content.TargetLanguage;
        var allTranslationIssues = new List<XliffIssueDto>();
        var systemPrompt = String.IsNullOrEmpty(customSystemPrompt) ? "Perform an LQA analysis and use the MQM error typology format using all 7 dimensions. " +
                           "Here is a brief description of the seven high-level error type dimensions: " +
                           "1. Terminology – errors arising when a term does not conform to normative domain or organizational terminology standards or when a term in the target text is not the correct, normative equivalent of the corresponding term in the source text. " +
                           "2. Accuracy – errors occurring when the target text does not accurately correspond to the propositional content of the source text, introduced by distorting, omitting, or adding to the message. " +
                           "3. Linguistic conventions  – errors related to the linguistic well-formedness of the text, including problems with grammaticality, spelling, punctuation, and mechanical correctness. " +
                           "4. Style – errors occurring in a text that are grammatically acceptable but are inappropriate because they deviate from organizational style guides or exhibit inappropriate language style. " +
                           "5. Locale conventions – errors occurring when the translation product violates locale-specific content or formatting requirements for data elements. " +
                           "6. Audience appropriateness – errors arising from the use of content in the translation product that is invalid or inappropriate for the target locale or target audience. " +
                           "7. Design and markup – errors related to the physical design or presentation of a translation product, including character, paragraph, and UI element formatting and markup, integration of text with graphical elements, and overall page or window layout. " +
                           "Provide a quality rating for each dimension from 0 (completely bad) to 10 (perfect). You are an expert linguist and your task is to perform a Language Quality Assessment on input sentences. " +
                           "Do not propose a fixed translation, only report on the errors. " +
                           "Formatting: use line spacing between each category. The category name should be bold. " : customSystemPrompt;

        if (glossary.Glossary != null)
        {
            systemPrompt +=
                "Use the provided glossary entries for the respective languages. If there are discrepancies " +
                "between the translation and glossary, note them in the 'Terminology' part of the report, " +
                "along with terminology problems not related to the glossary. ";
        }

        if (!String.IsNullOrEmpty(additionalPrompt))
        {
            systemPrompt += additionalPrompt;
        }

        var unitsToProcess = content.GetSegments().Where(x => x.State > 0).Where(x => (input.PostEditLockedSegments.HasValue && input.PostEditLockedSegments.Value) ? x.State != SegmentState.Final : true);

        var tuJson = System.Text.Json.JsonSerializer.Serialize(
          unitsToProcess.Select(x => new { x.Id, Source = x.GetSource(), Target = x.GetTarget() }),
          new JsonSerializerOptions { WriteIndented = true });

        var userPrompt = $"Here are the translation units from {sourceLanguage} into {targetLanguage}:\n" +
                         tuJson +
                         $"{(input.TargetAudience != null ? $"\nTarget audience: {input.TargetAudience}" : "")}";

        if (glossary.Glossary != null)
        {
            var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary, string.Join(';', unitsToProcess.Select(x => x.Source)));
            if (!string.IsNullOrEmpty(glossaryPromptPart))
            {
                userPrompt = userPrompt + glossaryPromptPart;
            }
        }

        return (userPrompt, systemPrompt);
    }

    [Action("Create MQM report", Description = "Perform an LQA Analysis on a translated file. The result will be in the MQM framework form.")]
    public async Task<GetMQMResponse> GenerateMQMReport(
        [ActionParameter] GetTranslationIssuesRequest input,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter][Display("Additional prompt instructions")] string? additionalPrompt,
        [ActionParameter][Display("System prompt (fully replaces MQM instructions)")] string? customSystemPrompt)
    {
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var content = await stream.ParseTransformationWithErrorHandling(input.File.Name);
        var model = input.AIModel;

        var (userPrompt, systemPrompt) = await CreatePrompts(input, customSystemPrompt, glossary, additionalPrompt, content);

        string response = "";
        var promptUsage = new UsageDto();

        try
        {
            (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt.ToString(), systemPrompt);
        }
        catch (Exception e)
        {
            throw new PluginApplicationException(e.Message);
        }
        return new GetMQMResponse
        {
            Report = response,
            Usage = promptUsage,
            SystemPrompt = systemPrompt
        };

    }

    [Action("Create MQM report in background", Description = "Perform an LQA Analysis on a translated file. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<BatchIdentifier> GenerateMQMReportInBackground(
    [ActionParameter] GetTranslationIssuesRequest input,
    [ActionParameter] GlossaryRequest glossary,
    [ActionParameter] PromptRequest promptRequest,
    [ActionParameter][Display("Additional prompt instructions")] string? additionalPrompt,
    [ActionParameter][Display("System prompt (fully replaces MQM instructions)")] string? customSystemPrompt)
    {
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var content = await Transformation.Parse(stream, input.File.Name);

        var (userPrompt, systemPrompt) = await CreatePrompts(input, customSystemPrompt, glossary, additionalPrompt, content);

        var jsonlMs = new MemoryStream();
        using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            var req = BatchHelper.BuildBatchRequestObject(userPrompt, systemPrompt, promptRequest, input.AIModel);
            var line = JsonConvert.SerializeObject(req, Formatting.None);
            await sw.WriteLineAsync(line);
        }

        var job = await CreateBatchRequest(jsonlMs, input.AIModel);

        return new BatchIdentifier
        {
            JobName = job.Name
        };

    }
}
