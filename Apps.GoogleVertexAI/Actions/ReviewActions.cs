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
    public async Task<ScoreXliffResponse> Score(
        [ActionParameter] AIModelRequest model,
        [ActionParameter] ScoreRequest input,
        [ActionParameter, Display("Prompt", Description = "Add any linguistic criteria for quality evaluation")] string? prompt,
        [ActionParameter] PromptRequest promptRequest,
        [ActionParameter, Display("Bucket size", Description = "Specify the number of translation units to be processed at once. Default value: 1500. (See our documentation for an explanation)")] int? bucketSize = 1500)
    {
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
            .SegmentStatesToEstimate?
            .Select(SegmentStateHelper.ToSegmentState)
            .ToList() ?? [SegmentState.Initial, SegmentState.Translated];
        var segmentsToEstimate = transformation
            .GetSegments()
            .Where(s => !string.IsNullOrEmpty(s.Id) && !string.IsNullOrEmpty(s.GetSource()) && !string.IsNullOrEmpty(s.GetTarget()))
            .Where(s => statesToEstimate.Contains(s.State ?? SegmentState.Initial))
            .ToList();
        var batches = segmentsToEstimate.Batch(bucketSize ?? 1500);
        var criteriaPrompt = string.IsNullOrEmpty(prompt)
            ? "accuracy, fluency, consistency, style, grammar and spelling"
            : prompt;
        var sourceLanguage = input.SourceLanguage ?? transformation.SourceLanguage;
        var targetLanguage = input.TargetLanguage ?? transformation.TargetLanguage;

        var results = new Dictionary<string, float>();
        var usage = new UsageDto();

        foreach (var batch in batches)
        {
            var systemPrompt =
                "You are a linguistic expert that should process the following texts according to the given instructions. Include in your response the ID of the sentence and the score number as a comma separated array of tuples without any additional information (it is crucial because your response will be deserialized programmatically).";

            var userPrompt =
                $"Your input is going to be a group of sentences in {sourceLanguage} and their translation into {targetLanguage}. " +
                "Only provide as output the ID of the sentence and the score number as a comma separated array of tuples. " +
                $"Place the tuples in a same line and separate them using semicolons, example for two assessments: `2,7.0;32,50.0`. The score number is a score from 1.0 to 100.0 assessing the quality of the translation, considering the following criteria: {criteriaPrompt}. Sentences: ";

            foreach (var segment in batch)
            {
                userPrompt += $"{segment.Id}: {segment.GetSource()} -> {segment.GetTarget()}\n";
            }

            var (result, promptUsage) = await ExecuteGeminiPrompt(promptRequest, model.AIModel, userPrompt, systemPrompt);

            usage += promptUsage;

            try
            {
                foreach (var r in result.Split(";"))
                {
                    var split = r.Split(",");
                    var id = split[0].Trim();
                    var score = float.Parse(split[1].Trim());
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

        var newSegmentState = SegmentStateHelper.ToSegmentState(input.NewState ?? string.Empty);
        var ShouldSaveScores = input.SaveScores ?? false;
        Func<float, bool> isPassingTreshold = input.ScoreThresholdComparison switch
        {
            null or "" => score => score >= input.Threshold,
            ">=" => score => score >= input.Threshold,
            ">" => score => score > input.Threshold,
            "=" => score => score == input.Threshold,
            "<" => score => score < input.Threshold,
            "<=" => score => score <= input.Threshold,
            _ => throw new PluginMisconfigurationException("The provided condition is not supported. Supported conditions are: >, >=, =, <, <=")
        };
        XNamespace itsNamespace = "http://www.w3.org/2005/11/its";

        foreach (var segment in segmentsToEstimate)
        {
            float score;

            if (!results.TryGetValue(segment.Id!, out score))
                continue;

            if (input.Threshold != null && isPassingTreshold(score) && newSegmentState != null)
                segment.State = newSegmentState;

            if (ShouldSaveScores)
            {
                segment.TargetAttributes.Add(new XAttribute(itsNamespace + "locQualityRatingScore", score.ToString()));

                if (input.Threshold != null)
                    segment.TargetAttributes.Add(new XAttribute(itsNamespace + "locQualityRatingScoreThreshold", input.Threshold.ToString() ?? "0.0"));
            }
        }

        var resultingXliff = transformation.Serialize();
        var rewrittenXliff = XliffNamespaceRewriter.RewriteTargets(resultingXliff); // TEMP fix until we add ratings into filters library
        return new ScoreXliffResponse
        {
            File = await fileManagementClient.UploadAsync(rewrittenXliff.ToStream(), "application/xliff+xml", input.File.Name),
            AverageScore = results.Average(x => x.Value),
            Usage = usage,
        };
    }
}
