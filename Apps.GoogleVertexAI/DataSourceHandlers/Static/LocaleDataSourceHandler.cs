using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using System.Globalization;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;
public class LocaleDataSourceHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(c => new DataSourceItem(c.Name, c.DisplayName));
    }
}