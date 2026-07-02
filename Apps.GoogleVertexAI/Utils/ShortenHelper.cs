using System.Text;
using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Helpers;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Utils;

public static class ShortenHelper
{
    public delegate Task<(string Result, UsageDto Usage)> ExecutePrompt(
        PromptRequest input, 
        string modelId, 
        string prompt, 
        string? systemPrompt, 
        object? schema);
    
    public static async Task<(ShortenResultDto shortenResult, Transformation transformation)> ShortenSingleFile(
        Transformation content,
        ShortenSettings settings,
        PromptRequest promptRequest,
        ExecutePrompt executePrompt)
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
                    var (response, promptUsage) = await executePrompt(
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

        var shortenResult = new ShortenResultDto
        {
            TotalUnitsCount = units.Count,
            UnitsWithRestrictionCount = unitsWithRestrictions.Count,
            UnitsMatchedFilterCount = unitsMatchingFilter.Count,
            UnitsOverLimitCount = candidates.Count,
            UnitsUpdatedCount = updatedCount,
            UnitsRemainingOverLimitCount = pending.Count,
            ProcessedBatchesCount = processedBatchesCount,
            RetryAttemptsCount = retryAttemptsCount,
            SystemPrompt = settings.SystemPrompt,
            PromptTemplate = settings.PromptTemplate,
            Usage = usage,
            ErrorMessages = errorMessages
        };

        return (shortenResult, content);
    }
}