using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Interfaces;
using Blackbird.Filters.Transformations;

namespace Apps.GoogleVertexAI.Helpers;

public static class TranslationUnitsPromptBuilder
{
    private const string AltTransElementName = "alt-trans";
    private const string PhraseTranslationMemoryOrigin = "memsource-tm";

    public static TranslationUnitsPrompt Build(Transformation content, bool postEditLockedSegments, bool includeTranslationMemorySuggestions)
    {
        var segmentsToReview = content.GetUnits()
            .SelectMany(unit => unit.Segments.Select(segment => (Unit: unit, Segment: segment)))
            .Where(x => x.Segment.State > 0 || x.Segment.State is null)
            .Where(x => !postEditLockedSegments || x.Segment.State != SegmentState.Final)
            .ToList();

        var promptSegments = segmentsToReview
            .Select(x => ToPromptSegment(x.Unit, x.Segment, includeTranslationMemorySuggestions))
            .ToList();

        var json = JsonSerializer.Serialize(
            promptSegments.Select(x => x.Value),
            new JsonSerializerOptions { WriteIndented = true });

        return new TranslationUnitsPrompt(
            json,
            segmentsToReview.Select(x => x.Segment).ToList(),
            promptSegments.Any(x => x.HasSuggestions));
    }

    private static (object Value, bool HasSuggestions) ToPromptSegment(Unit unit, Segment segment, bool includeTranslationMemorySuggestions)
    {
        var suggestions = includeTranslationMemorySuggestions ? GetTranslationMemorySuggestions(unit, segment) : [];

        if (suggestions.Count == 0)
            return (new { segment.Id, Source = segment.GetSource(), Target = segment.GetTarget() }, false);

        return (new { segment.Id, Source = segment.GetSource(), Target = segment.GetTarget(), TranslationMemorySuggestions = suggestions }, true);
    }

    private static List<TranslationMemorySuggestionDto> GetTranslationMemorySuggestions(Unit unit, Segment segment)
    {
        var isSingleSegmentUnit = unit.Segments.Count == 1;

        return unit.Other.OfType<XElement>()
            .Where(x => x.Name.LocalName == AltTransElementName && x.Attribute("origin")?.Value == PhraseTranslationMemoryOrigin)
            .Where(x => BelongsToSegment(x, segment, isSingleSegmentUnit))
            .Select(x => ToSuggestion(x, unit.SegmentCoder))
            .Where(x => !string.IsNullOrWhiteSpace(x.Target))
            .ToList();
    }

    private static bool BelongsToSegment(XElement altTrans, Segment segment, bool isSingleSegmentUnit)
    {
        var segmentId = altTrans.Attribute("mid")?.Value;
        return segmentId is null ? isSingleSegmentUnit : segmentId == segment.Id;
    }

    private static TranslationMemorySuggestionDto ToSuggestion(XElement altTrans, ISegmentCoder segmentCoder)
    {
        var targetElement = altTrans.Elements().FirstOrDefault(x => x.Name.LocalName == "target");
        var target = targetElement is null ? string.Empty : EncodeLikeSegmentText(targetElement, segmentCoder);
        return new TranslationMemorySuggestionDto(target, FormatMatchQuality(altTrans.Attribute("match-quality")?.Value));
    }

    private static string EncodeLikeSegmentText(XElement targetElement, ISegmentCoder segmentCoder)
    {
        var innerXml = string.Concat(targetElement.Nodes().Select(x => x.ToString(SaveOptions.DisableFormatting)));
        return segmentCoder.SerializeSegment(segmentCoder.DeserializeSegment(innerXml));
    }

    private static string? FormatMatchQuality(string? matchQuality)
    {
        if (!double.TryParse(matchQuality, NumberStyles.Float, CultureInfo.InvariantCulture, out var quality))
            return matchQuality;

        return $"{Math.Round(quality * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";
    }
}
