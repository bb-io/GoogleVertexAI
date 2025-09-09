using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using MoreLinq;
using System.Xml.Linq;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Review")]
public class ReviewActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiInvocable(invocationContext)
{
    [Action("Score segments", Description = "Gets segment and file level quality scores for XLIFF files")]
    public async Task<ScoreResponse> Score(
        [ActionParameter] AIModelRequest model,
        [ActionParameter] ScoreRequest input,
        [ActionParameter, Display("Prompt", Description = "Add any linguistic criteria for quality evaluation")] string? prompt,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = 1500)
    {
        // TODO Apply agreements made on standup
        var fileStream = await fileManagementClient.DownloadAsync(input.File);
        Transformation transformation; 
        
        try
        {
            transformation = await Transformation.Parse(fileStream, input.File.Name);
        }
        catch (Exception)
        {
            throw new PluginApplicationException("The provided file is not supported. XLIFF, HTML and plain text files are supported at the moment.");
        }

        var statesToEstimate = input
            .EstimateUnitsWhereAllSegmentStates?
            .Select(SegmentStateHelper.ToSegmentState)
            .Where(state => state != null)
            .Select(state => state!.Value)
            .ToList() ?? [SegmentState.Initial, SegmentState.Translated];
        var unitsToEstimate = transformation
            .GetUnits()
            .Where(u => u.Id != null)
            .Where(ReviewUtils.HasTranslatedContent)
            .Where(u => ReviewUtils.HasSegmentWithState(u, statesToEstimate))
            .ToList();
        var batches = unitsToEstimate.Batch(bucketSize ?? 1500);
        var criteriaPrompt = string.IsNullOrEmpty(prompt)
            ? "accuracy, fluency, consistency, style, grammar and spelling"
            : prompt;
        var sourceLanguage = input.SourceLanguage ?? transformation.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? transformation.TargetLanguage;

        var results = new Dictionary<string, double>();
        var usage = new UsageDto();

        if (unitsToEstimate.Count == 0)
        {
            return new ScoreResponse
            {
                File = input.File,
                TotalUnits = transformation.GetUnits().Count(),
                TotalUnitsProcessed = 0,
                TotalUnitsUnderThreshhold = 0,
                TotalSegmentsFinalized = 0,
                AverageScore = 0,
                PercentageUnitsUnderThreshold = 0,
                Usage = usage,
            };
        }

        foreach (var batch in batches)
        {
            var systemPrompt =
                "You are a linguistic expert that should process the following texts according to the given instructions. Include in your response the ID of the text unit and the score number as a comma separated array of tuples without any additional information (it is crucial because your response will be deserialized programmatically).";

            var userPrompt =
                $"Your input is going to be a group of text units in {sourceLanguage} and their translation into {targetLanguage}. " +
                $"Only provide as output the ID of the text unit like `{batch[0].Id}` and the score number as a comma separated array of tuples. " +
                $"Place the tuples in a same line and separate them using semicolons, example for two assessments: `2,7.0;32,50.0`. The score number is a score from 1.0 to 100.0 assessing the quality of the translation, considering the following criteria: {criteriaPrompt}. Text units: ";

            foreach (var unit in batch)
            {
                userPrompt += $"{unit.Id}: {ReviewUtils.GetUnitSource(unit)} -> {ReviewUtils.GetUnitTarget(unit)}\n";
            }

            var (result, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model.AIModel, userPrompt, systemPrompt);

            usage += promptUsage;

            try
            {
                foreach (var r in result.Trim('`').Split(";"))
                {
                    if (string.IsNullOrWhiteSpace(r))
                        continue;

                    var split = r.Split(",");
                    var id = split[0].Trim();
                    var score = double.Parse(split[1].Trim());
                    results.Add(id, score);
                }
            }
            catch (Exception ex)
            {
                throw new PluginApplicationException(
                    $"Failed to parse the LLM response for a batch.\n" +
                    $"Original LLM response:\n{result}\n" +
                    $"Error detail: {ex.Message}", ex);
            }
        }

        var newSegmentState = SegmentStateHelper.ToSegmentState(input.NewState ?? string.Empty) ?? SegmentState.Reviewed;
        var ShouldSaveScores = input.SaveScores ?? true;
        var totalUnitsProcessed = 0;
        var totalUnitsUnderThreshhold = 0;
        var totalSegmentsFinalized = 0;
        var totalScore = 0.0;

        foreach (var unit in unitsToEstimate)
        {
            if (!results.TryGetValue(unit.Id!, out var score))
                continue;

            if (ShouldSaveScores)
            {
                unit.Quality.Score = score;
                unit.Quality.ScoreThreshold = input.Threshold;
            }

            if (score >= input.Threshold && input.Threshold != null)
            {
                foreach (var segment in unit.Segments)
                {
                    if (!statesToEstimate.Contains(segment.State ?? SegmentState.Initial))
                        continue;

                    segment.State = newSegmentState;
                    totalSegmentsFinalized++;
                }
            }
            else if (input.Threshold != null)
            {
                totalUnitsUnderThreshhold++;
            }

            totalScore += score;
            totalUnitsProcessed++;
        }

        var xliffStream = transformation.Serialize().ToStream();
        return new ScoreResponse
        {
            File = await fileManagementClient.UploadAsync(xliffStream, "application/xliff+xml", input.File.Name),
            TotalUnits = transformation.GetUnits().Count(),
            TotalUnitsProcessed = totalUnitsProcessed,
            TotalUnitsUnderThreshhold = totalUnitsUnderThreshhold,
            TotalSegmentsFinalized = totalSegmentsFinalized,
            AverageScore = totalUnitsProcessed > 0 ? (totalScore / totalUnitsProcessed) : totalScore,
            PercentageUnitsUnderThreshold = totalUnitsProcessed > 0 ? ((double)totalUnitsUnderThreshhold / (double)totalUnitsProcessed) : totalUnitsUnderThreshhold,
            Usage = usage,
        };
    }
}
