using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;

namespace Apps.GoogleVertexAI.Utils;

public class ReviewUtils
{
    public static string GetUnitSource(Unit unit)
    {
        var sources = unit.Segments.Select(s => s.GetSource());
        return string.Join(string.Empty, sources);
    }

    public static string GetUnitTarget(Unit unit)
    {
        var targets = unit.Segments.Select(s => s.GetTarget());
        return string.Join(string.Empty, targets);
    }

    public static bool HasTranslatedContent(Unit unit)
    {
        return unit.Segments.Any(s => !string.IsNullOrEmpty(s.GetSource()))
            && unit.Segments.Any(s => !string.IsNullOrEmpty(s.GetTarget()));
    }

    public static bool HasSegmentWithState(Unit unit, IEnumerable<SegmentState> states)
    {
        return states
            .Intersect(unit.Segments.Select(s => s.State ?? SegmentState.Initial))
            .Any();
    }
}
