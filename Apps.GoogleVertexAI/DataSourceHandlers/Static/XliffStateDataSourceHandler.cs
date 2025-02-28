using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class XliffStateDataSourceHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new DataSourceItem("final", "Final" ),
            new DataSourceItem("needs-adaptation", "Needs adaptation" ),
            new DataSourceItem("needs-l10n", "Needs l10n" ),
            new DataSourceItem("needs-review-adaptation", "Needs review adaptation" ),
            new DataSourceItem("needs-review-l10n", "Needs review l10n" ),
            new DataSourceItem("needs-review-translation", "Needs review translation" ),
            new DataSourceItem("needs-translation", "Needs translation" ),
            new DataSourceItem("new", "New" ),
            new DataSourceItem("signed-off", "Signed off" ),
            new DataSourceItem("translated", "Translated" ),
        };
    }
}