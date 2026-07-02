using Apps.GoogleVertexAI.Models.Requests;
using Blackbird.Filters.Enums;

namespace Apps.GoogleVertexAI.Models.Dto;

public record ShortenSettings(
    string AiModel,
    int BatchSize,
    int RetryCount,
    string SystemPrompt,
    IReadOnlySet<SegmentState> StatesToProcess,
    string? AdditionalInstructions,
    string PromptTemplate)
{
    public const int DefaultBatchSize = 1500;
    public const int DefaultRetryCount = 3;
    
    public const string DefaultSystemPrompt =
        "You are a linguistic expert. Shorten translated target text so the full target " +
        "for each unit fits the supplied maximum grapheme count. " +
        "Preserve meaning, formality, style, locale conventions, and all inline placeholders or tags. " +
        "Return only the structured JSON output requested by the schema.";
    
    public const string DefaultPromptTemplate =
        "Shorten target text for each unit from {sourceLanguage} into {targetLanguage}. " +
        "For every unit, return exactly one object with the same id and the same number of target segments. " +
        "The concatenated target segments for each unit must be no longer than maxGraphemes Unicode graphemes. " +
        "Keep segment order. Preserve inline placeholders and tags exactly. Additional instructions: {additionalInstructions}. " +
        "Units JSON shape: [{ id, maxGraphemes, sourceSegments, targetSegments }].";
    
    public static ShortenSettings Build(
        ShortenContentRequest input,
        string? additionalInstructions,
        string? customSystemPrompt)
    {
        var selectedStates = input.SegmentStates?
            .Select(SegmentStateHelper.ToSegmentState)
            .Where(state => state != null)
            .Select(state => state!.Value)
            .ToHashSet();

        IReadOnlySet<SegmentState> statesToProcess = selectedStates is { Count: > 0 }
            ? selectedStates
            : [SegmentState.Initial, SegmentState.Translated];

        return new ShortenSettings(
            input.AIModel,
            input.BatchSize.GetValueOrDefault(DefaultBatchSize),
            input.RetryCount.GetValueOrDefault(DefaultRetryCount),
            string.IsNullOrWhiteSpace(customSystemPrompt) ? DefaultSystemPrompt : customSystemPrompt,
            statesToProcess,
            additionalInstructions,
            DefaultPromptTemplate);
    }
}