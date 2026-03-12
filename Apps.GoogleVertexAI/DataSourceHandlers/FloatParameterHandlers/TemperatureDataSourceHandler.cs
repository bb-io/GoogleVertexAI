using System.Globalization;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers;

public class TemperatureDataSourceHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return
        [
            new DataSourceItem(0.1f.ToString("0.00", CultureInfo.InvariantCulture), "0.1 | Governed"),
            new DataSourceItem(0.3f.ToString("0.00", CultureInfo.InvariantCulture), "0.3 | Balanced"),
            new DataSourceItem(0.5f.ToString("0.00", CultureInfo.InvariantCulture), "0.5 | Expressive"),
            new DataSourceItem(0.6f.ToString("0.00", CultureInfo.InvariantCulture), "0.6 | Exploratory"),
            new DataSourceItem(0.8f.ToString("0.00", CultureInfo.InvariantCulture), "0.8 | Experimental"),
        ];
    }
}