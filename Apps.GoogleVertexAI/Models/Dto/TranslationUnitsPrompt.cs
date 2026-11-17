using Blackbird.Filters.Transformations;

namespace Apps.GoogleVertexAI.Models.Dto;

public record TranslationUnitsPrompt(string Json, IReadOnlyList<Segment> Segments, bool HasTranslationMemorySuggestions);

public record TranslationMemorySuggestionDto(string Target, string? MatchQuality);
