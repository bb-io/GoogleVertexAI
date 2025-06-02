using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class XliffStateDataSourceHandler : IStaticDataSourceItemHandler
{
    private static readonly Dictionary<string, string> Data = new()
    {
        {"final", "Final" },
        {"needs-adaptation", "Needs adaptation" },
        {"needs-l10n", "Needs l10n" },
        {"needs-review-adaptation", "Needs review adaptation" },
        {"needs-review-l10n", "Needs review l10n" },
        {"needs-review-translation", "Needs review translation" },
        {"needs-translation", "Needs translation" },
        {"new", "New" },
        {"signed-off", "Signed off" },
        {"translated", "Translated" },
    };

    public IEnumerable<DataSourceItem> GetData()
    {
        return Data.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}