using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
using System.Globalization;

namespace Apps.GoogleVertexAI.Models.Dto;

public class ShortenCandidate
{
    public int Id { get; }
    public Unit Unit { get; }
    public List<Segment> Segments { get; }
    public int MaximumGraphemes { get; }
    public List<string> WorkingTargets { get; set; }
    public List<string>? AcceptedTargets { get; set; }
    public string? LastError { get; set; }

    public int CurrentGraphemeCount => CountGraphemes(string.Concat(WorkingTargets));

    public string DisplayId => Unit.Id ?? Id.ToString(CultureInfo.InvariantCulture);

    public ShortenCandidate(int id, Unit unit, int maximumGraphemes)
    {
        Id = id;
        Unit = unit;
        Segments = unit.Segments.Where(segment => !segment.IsIgnorbale).ToList();
        MaximumGraphemes = maximumGraphemes;
        WorkingTargets = Segments.Select(segment => segment.GetTarget()).ToList();
    }

    private static int CountGraphemes(string? value)
        => StringInfo.ParseCombiningCharacters(value ?? string.Empty).Length;
}
