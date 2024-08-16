using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class XliffStateDataSourceHandler : IStaticDataSourceHandler
{
    public Dictionary<string, string> GetData()
    {
        return new Dictionary<string, string>
        {
            { "final", "Final" },
            { "needs-adaptation", "Needs adaptation" },
            { "needs-l10n", "Needs l10n" },
            { "needs-review-adaptation", "Needs review adaptation" },
            { "needs-review-l10n", "Needs review l10n" },
            { "needs-review-translation", "Needs review translation" },
            { "needs-translation", "Needs translation" },
            { "new", "New" },
            { "signed-off", "Signed off" },
            { "translated", "Translated" },
        };
    }
}