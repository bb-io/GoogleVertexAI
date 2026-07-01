using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Utils;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json;
using System.Text;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Extensions;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Editing")]
public class EditActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) 
    : VertexAiInvocable(invocationContext)
{
    private const string PromptTemplate =
        "Shorten target text for each unit from {sourceLanguage} into {targetLanguage}. " +
        "For every unit, return exactly one object with the same id and the same number of target segments. " +
        "The concatenated target segments for each unit must be no longer than maxGraphemes Unicode graphemes. " +
        "Keep segment order. Preserve inline placeholders and tags exactly. Additional instructions: {additionalInstructions}. " +
        "Units JSON shape: [{ id, maxGraphemes, sourceSegments, targetSegments }].";
    
    [Action("Shorten content", Description = "Shorten XLIFF target content to fit slr:sizeRestriction values.")]
    public async Task<ShortenContentResponse> ShortenContent(
        [ActionParameter] ShortenContentRequest input,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to apply when shortening.")] string? additionalInstructions,
        [ActionParameter, Display("System prompt (fully replaces shortening instructions)")] string? customSystemPrompt,
        [ActionParameter] PromptRequest promptRequest)
    {
        var files = input.Files.ToList();
        
        var settings = ShortenSettings.Build(input, additionalInstructions, customSystemPrompt);
        if (settings.BatchSize <= 0)
            throw new PluginApplicationException("Batch size must be greater than zero.");
        if (settings.RetryCount < 0)
            throw new PluginApplicationException("Retries must be zero or greater.");
        
        var results = new ShortenResultDto[files.Count];
        
        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 5 },
            async (i, _) =>
            {
                var file = files[i];
                
                try
                {
                    var stream = await fileManagementClient.DownloadAsync(file);
                    var loadResult = Transformation.Load(stream, file.Name);
                    if (!loadResult.Success)
                        throw new PluginMisconfigurationException(loadResult.Error);
                    
                    results[i] = await ShortenSingleFile(loadResult.Value, settings, promptRequest);
                }
                catch (Exception ex)
                {
                    results[i] = new ShortenResultDto
                    {
                        File = file,
                        SystemPrompt = settings.SystemPrompt,
                        PromptTemplate = PromptTemplate,
                        ErrorMessages = [$"Failed to process '{file.Name}': {ex.Message}"]
                    };
                }
            });

        return new(results);
    }

    [BlueprintActionDefinition(BlueprintAction.EditFile)]
    [Action("Edit", 
        Description = "Edit a translation. This action assumes you have previously translated content in Blackbird through any translation action.")]
    public async Task<FileEditResponse> EditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of source texts to be translated at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = null)
    {
        var batchSize = bucketSize ?? 1500;
        var model = input.AIModel;
        var result = new FileEditResponse();
        var stream = await fileManagementClient.DownloadAsync(input.File);
        
        var loadResult = Transformation.Load(stream, input.File.Name);
        if (!loadResult.Success)
            throw new PluginMisconfigurationException(loadResult.Error);
        var content = loadResult.Value;

        var counter = 1;        
        var errorMessages = new List<string>();

        async Task<IEnumerable<string?>> BatchEdit(IEnumerable<(Unit Unit, Segment Segment)> batch)
        {
            var batchList = batch.ToList();

            var inputObjects = batchList.Select((x, i) => new 
            { 
                id = i, 
                source = x.Segment.GetSource(),
                target = x.Segment.GetTarget()
            });
            var segmentsJson = JsonConvert.SerializeObject(inputObjects);
            
            string systemPrompt = 
                "You are a linguistic expert that should process the following texts according to the given instructions.";
        
            var userPrompt = new StringBuilder();
            userPrompt.AppendLine(
                $"Your input consists of sentences in {content.SourceLanguage} with their current translations into {content.TargetLanguage}. " +
                "Review and edit the translated target text as necessary to ensure it is a correct and accurate translation of the source text. " +
                "If you see XML tags in the source, you MUST include them in the target text. Do not delete or modify them. " +
                "Return ONLY a JSON array of objects containing the edited translations in the exact same order. " +
                $"Do not skip any entries. {prompt}");
            userPrompt.AppendLine();
            userPrompt.AppendLine($"Original texts: {segmentsJson}");
            
            if (glossary?.Glossary != null)
            {
                var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(
                    fileManagementClient, 
                    glossary.Glossary, 
                    segmentsJson);
            
                if (!string.IsNullOrWhiteSpace(glossaryPromptPart))
                {
                    userPrompt.AppendLine();
                    userPrompt.AppendLine(
                        "Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                        "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                        "If a term has variations or synonyms, consider them and choose the most appropriate " +
                        "translation to maintain consistency and precision.");
                    userPrompt.AppendLine(glossaryPromptPart);
                }
            }
            
            var (response, promptUsage) = await ExecuteGeminiPrompt(
                promptRequest, 
                model, 
                userPrompt.ToString(), 
                systemPrompt,
                ResponseSchemas.IdTranslationArray);

            try
            {
                var parsedResults = JsonConvert.DeserializeObject<TranslationResultDto[]>(response) ?? 
                                    throw new PluginApplicationException(
                                        "The Gemini API returned an empty or null JSON array.");

                if (parsedResults.Length != batchList.Count)
                {
                    errorMessages.Add(
                        $"The response from the Gemini API (batch number: {counter}) was incomplete. " +
                        $"Got {parsedResults.Length} results, but expected {batchList.Count}. " +
                        "Try to reduce the batch size.");
                }

                return parsedResults.OrderBy(x => x.Id).Select(x => x.Translation);
            }
            catch (Exception ex)
            {
                InvocationContext.Logger?.LogError(
                    $"[GoogleGemini] Failed to parse response: {ex.Message}; Response: {response}", []);
                throw new PluginApplicationException(
                    $"Failed to parse the response from Gemini API. " +
                    $"The response format might be invalid or incomplete. " +
                    $"Error: {ex.Message}", 
                    ex);
            }
            finally
            {
                counter++;
            }
        }

        var allUnits = content.GetUnits().ToList();
        result.TotalSegmentsCount = allUnits.SelectMany(x => x.Segments).Count();
        var editableUnits = allUnits
            .Where(u => u.Segments.Any(s => !s.IsIgnorbale && s.State == SegmentState.Translated))
            .ToList();
        result.TotalSegmentsReviewed = editableUnits
            .SelectMany(u => u.Segments)
            .Count(s => !s.IsIgnorbale && s.State == SegmentState.Translated);

        var processedBatches = await editableUnits.Batch(batchSize).Process(BatchEdit);
        result.ProcessedBatchesCount = counter - 1;

        var updatedCount = 0;
        foreach (var (unit, translations) in processedBatches)
        {
            foreach (var (segment, translation) in translations)
            {
                if (translation is not null && segment.State == SegmentState.Translated)
                {
                    if (segment.GetTarget() != translation)
                    {
                        updatedCount++;
                        segment.SetTarget(translation);
                    }
                    segment.State = SegmentState.Reviewed;
                }
            }
        }

        result.TotalSegmentsUpdated = updatedCount;

        if (input.OutputFileHandling == "original")
        {
            var targetContentLoadResult = content.Target();
            if (!targetContentLoadResult.Success)
                throw new PluginMisconfigurationException(targetContentLoadResult.Error);

            var targetContent = targetContentLoadResult.Value;
            
            result.File = await fileManagementClient.UploadAsync(
                targetContent.ToStream(), 
                targetContent.OriginalMediaType, 
                targetContent.OriginalName);
        } 
        else
        {
            result.File = await fileManagementClient.UploadAsync(
                content.Serialize().ToStream(), 
                MediaTypes.Xliff2, 
                input.File.Name.ToXliffFileName());
        }

        result.ErrorMessages = errorMessages;
        return result;
    }

    [Action("Edit in background", 
        Description = "Edit file content retrieved from a CMS or file storage. Use in conjunction with a checkpoint to get the result of this long running background job.")]
    public async Task<StartBatchResponse> BatchEditContent(
        [ActionParameter] EditFileRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] BucketRequest bucketRequest)
    {
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var loadResult = Transformation.Load(stream, input.File.Name);
        if (!loadResult.Success)
            throw new PluginMisconfigurationException(loadResult.Error);

        var content = loadResult.Value;
        var segmentList = content.GetUnits().SelectMany(x => x.Segments).GetSegmentsForEditing().ToList();

        var glossaryStructure = await GlossaryHelper.ParseGlossary(fileManagementClient, glossary?.Glossary);

        var systemPrompt = "You are a linguistic expert that should process the following texts according to the given instructions";

        var jsonlMs = new MemoryStream();
        await using (var sw = new StreamWriter(jsonlMs, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            var bucketSize = bucketRequest.GetBucketSizeOrDefault();

            for (int i = 0; i < segmentList.Count; i += bucketSize)
            {
                var bucketSegments = segmentList.Skip(i).Take(bucketSize).ToList();
                var inputObjects = bucketSegments.Select((s, index) => new 
                { 
                    id = i + index, 
                    source = s.GetSource(),
                    target = s.GetTarget()
                });
                var segmentsJson = JsonConvert.SerializeObject(inputObjects);
                
                var userPrompt = new StringBuilder();
                userPrompt.AppendLine(
                    $"Your input consists of sentences in {content.SourceLanguage} with their current translations into {content.TargetLanguage}. " +
                    "Review and edit the translated target text as necessary to ensure it is a correct and accurate translation of the source text. " +
                    "If you see XML tags in the source, you MUST include them in the target text. Do not delete or modify them. " +
                    "Return the edited translations in the specified JSON schema structure. " +
                    $"Do not skip any entries. {prompt}");
                userPrompt.AppendLine();
                userPrompt.AppendLine($"Original texts: {segmentsJson}");

                if (glossaryStructure != null)
                {
                    var glossaryPromptPart = GlossaryHelper.GetGlossaryPromptPart(glossaryStructure, segmentsJson);

                    if (!string.IsNullOrWhiteSpace(glossaryPromptPart))
                    {
                        userPrompt.AppendLine();
                        userPrompt.AppendLine(
                            "Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                            "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                            "If a term has variations or synonyms, consider them and choose the most appropriate " +
                            "translation to maintain consistency and precision.");
                        userPrompt.AppendLine(glossaryPromptPart);
                    }
                }

                var req = BatchHelper.BuildBatchRequestObject(
                    userPrompt.ToString(), 
                    systemPrompt, 
                    promptRequest, 
                    input.AIModel, 
                    ResponseSchemas.IdTranslationArray); 
            
                var line = JsonConvert.SerializeObject(req, Formatting.None);
                await sw.WriteLineAsync(line);
            }
        }

        var job = await CreateBatchRequest(jsonlMs, input.AIModel);
        content.MetaData.Add(new Metadata("background-type", "edit") { Category = [Meta.Categories.Blackbird] });
        
        var file = await fileManagementClient.UploadAsync(
            content.Serialize().ToStream(),
            MediaTypes.Xliff2,
            input.File.Name.ToXliffFileName());

        return new StartBatchResponse
        {
            JobName = job.Name,
            TransformationFile = file
        };
    }
        
    [BlueprintActionDefinition(BlueprintAction.EditText)]
    [Action("Edit text", Description = "Review translated text and generate an edited version.")]
    public async Task<TextEditResponse> EditText(
        [ActionParameter] EditTextRequest input,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary)
    {
        var model = input.AIModel;
        var systemPrompt =
            $"You are receiving a source text{(input.SourceLanguage != null ? $" written in {input.SourceLanguage} " : "")}" +
            $"that was translated into target text{(input.TargetLanguage != null ? $" written in {input.TargetLanguage}" : "")}. " +
            "Review the target text and respond with edits of the target text as necessary. If no edits required, respond with target text. ";

        if (glossary.Glossary != null)
            systemPrompt +=
                " Enhance the target text by incorporating relevant terms from our glossary where applicable. " +
                "Ensure that the translation aligns with the glossary entries for the respective languages. " +
                "If a term has variations or synonyms, consider them and choose the most appropriate " +
                "translation to maintain consistency and precision. If the translation already aligns " +
                "with the glossary, no edits are required.";

        if (prompt != null)
            systemPrompt = $"{systemPrompt} {prompt}";

        var userPrompt = @$"
            Source text: 
            {input.SourceText}

            Target text: 
            {input.TargetText}
        ";

        if (glossary.Glossary != null)
        {
            var glossaryPromptPart = await GlossaryHelper.GetGlossaryPromptPart(fileManagementClient, glossary.Glossary, input.SourceText);
            if (glossaryPromptPart != null)
            {
                userPrompt +=
                    "\nEnhance the localized text by incorporating relevant terms from our glossary where applicable. " +
                    "If you encounter terms from the glossary in the text, ensure that the localized text aligns " +
                    "with the glossary entries for the respective languages. If a term has variations or synonyms, " +
                    "consider them and choose the most appropriate translation from the glossary to maintain " +
                    $"consistency and precision. {glossaryPromptPart}";
            }
        }

        userPrompt += "Localized text: ";

        var (response, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model, userPrompt, systemPrompt);

        return new()
        {
            EditedText = response.Trim(),
        };
    }
    
    private async Task<ShortenResultDto> ShortenSingleFile(
        Transformation content,
        ShortenSettings settings,
        PromptRequest promptRequest)
    {
        var errorMessages = new List<string>();
        var usage = new UsageDto();
        
        var units = content.GetUnits().ToList();
        var unitsWithRestrictions = units
            .Select(unit => new { Unit = unit, MaximumGraphemes = unit.SizeRestrictions?.MaximumSize })
            .Where(x => x.MaximumGraphemes is > 0)
            .ToList();
        var unitsMatchingFilter = unitsWithRestrictions
            .Where(x =>
            {
                var segments = x.Unit.Segments.Where(segment => !segment.IsIgnorbale).ToList();
                return segments.Count > 0
                    && segments.All(segment => settings.StatesToProcess.Contains(segment.State ?? SegmentState.Initial));
            })
            .ToList();
        
        var candidates = unitsMatchingFilter
            .Select((x, index) => new ShortenCandidate(index, x.Unit, x.MaximumGraphemes!.Value))
            .Where(x => x.CurrentGraphemeCount > x.MaximumGraphemes)
            .ToList();

        var pending = candidates.ToList();
        var processedBatchesCount = 0;
        var retryAttemptsCount = 0;

        for (var attempt = 0; attempt <= settings.RetryCount && pending.Count > 0; attempt++)
        {
            if (attempt > 0)
                retryAttemptsCount++;

            var nextPending = new List<ShortenCandidate>();
            for (var i = 0; i < pending.Count; i += settings.BatchSize)
            {
                var batch = pending.Skip(i).Take(settings.BatchSize).ToList();
                processedBatchesCount++;

                var inputObjects = batch.Select(x => new
                {
                    id = x.Id,
                    maxGraphemes = x.MaximumGraphemes,
                    sourceSegments = x.Segments.Select(segment => segment.GetSource()).ToArray(),
                    targetSegments = x.WorkingTargets.ToArray()
                });
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine(
                    $"Shorten target text for each unit from {content.SourceLanguage ?? "the source language"} into {content.TargetLanguage ?? "the target language"}. " +
                    "For every unit, return exactly one object with the same id and the same number of target segments. " +
                    "The concatenated target segments for each unit must be no longer than maxGraphemes Unicode graphemes. " +
                    "Keep segment order. Preserve inline placeholders and tags exactly.");

                if (!string.IsNullOrWhiteSpace(settings.AdditionalInstructions))
                    promptBuilder.AppendLine($"Additional instructions: {settings.AdditionalInstructions}");

                if (attempt > 0)
                    promptBuilder.AppendLine("Previous output was still too long for these units. Shorten the provided targetSegments further.");

                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"Units: {JsonConvert.SerializeObject(inputObjects)}");
                var prompt = promptBuilder.ToString();

                ShortenContentResultDto[] results;
                try
                {
                    var (response, promptUsage) = await ExecuteGeminiPrompt(
                        promptRequest, 
                        settings.AiModel, 
                        prompt, 
                        settings.SystemPrompt, 
                        ResponseSchemas.IdTargetsArray);

                    usage += promptUsage;
                    results = JsonConvert.DeserializeObject<ShortenContentResultDto[]>(response) ??
                        throw new PluginApplicationException("The Gemini API returned an empty or null JSON array.");
                }
                catch (Exception ex)
                {
                    foreach (var candidate in batch.Where(x => x.CurrentGraphemeCount > x.MaximumGraphemes))
                    {
                        candidate.LastError = 
                            $"Failed to process shortening batch {processedBatchesCount} on attempt {attempt + 1}: {ex.Message}";
                        nextPending.Add(candidate);
                    }
                    continue;
                }

                foreach (var duplicateId in results.GroupBy(x => x.Id).Where(x => x.Count() > 1).Select(x => x.Key))
                    errorMessages.Add($"Gemini response included duplicate result id {duplicateId}.");

                var resultsById = results.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
                foreach (var candidate in batch)
                {
                    if (!resultsById.TryGetValue(candidate.Id, out var result))
                    {
                        candidate.LastError = $"Gemini response did not include unit {candidate.DisplayId}.";
                        nextPending.Add(candidate);
                        continue;
                    }

                    if (result.Targets.Length != candidate.Segments.Count)
                    {
                        candidate.LastError =
                            $"Gemini response for unit {candidate.DisplayId} had {result.Targets.Length} target segments, expected {candidate.Segments.Count}.";
                        nextPending.Add(candidate);
                        continue;
                    }

                    candidate.WorkingTargets = result.Targets.ToList();
                    if (candidate.CurrentGraphemeCount <= candidate.MaximumGraphemes)
                    {
                        candidate.AcceptedTargets = candidate.WorkingTargets.ToList();
                        candidate.LastError = null;
                        continue;
                    }

                    nextPending.Add(candidate);
                }
            }

            pending = nextPending.Where(x => x.CurrentGraphemeCount > x.MaximumGraphemes).ToList();
        }

        var updatedCount = 0;
        foreach (var candidate in candidates.Where(x => x.AcceptedTargets is not null))
        {
            var changed = false;
            for (var i = 0; i < candidate.Segments.Count; i++)
            {
                var target = candidate.AcceptedTargets![i];
                if (candidate.Segments[i].GetTarget() == target)
                    continue;

                candidate.Segments[i].SetTarget(XmlHelpers.EnsureXmlSafe(target));
                changed = true;
            }

            if (changed)
                updatedCount++;
        }

        foreach (var candidate in pending)
        {
            if (!string.IsNullOrWhiteSpace(candidate.LastError))
                errorMessages.Add(candidate.LastError);

            errorMessages.Add(
                $"Unit {candidate.DisplayId} remains over the {candidate.MaximumGraphemes} " +
                $"grapheme limit after {settings.RetryCount} retries.");
        }

        var resultFile = await fileManagementClient.UploadAsync(
            content.Serialize().ToStream(), 
            MediaTypes.Xliff2, 
            content.BilingualFileName.ToXliffFileName());

        return new ShortenResultDto
        {
            File = resultFile,
            TotalUnitsCount = units.Count,
            UnitsWithRestrictionCount = unitsWithRestrictions.Count,
            UnitsMatchedFilterCount = unitsMatchingFilter.Count,
            UnitsOverLimitCount = candidates.Count,
            UnitsUpdatedCount = updatedCount,
            UnitsRemainingOverLimitCount = pending.Count,
            ProcessedBatchesCount = processedBatchesCount,
            RetryAttemptsCount = retryAttemptsCount,
            SystemPrompt = settings.SystemPrompt,
            PromptTemplate = PromptTemplate,
            Usage = usage,
            ErrorMessages = errorMessages
        };
    }
}
