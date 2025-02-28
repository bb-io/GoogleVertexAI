using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class ConditionDataSourceHandler : IStaticDataSourceItemHandler
{
    private static List<DataSourceItem> ConditionList => new()
    {
        new DataSourceItem(">", "Score is above threshold"),
        new DataSourceItem(">=", "Score is above or equal threshold" ),
        new DataSourceItem("=", "Score is same as threshold" ),
        new DataSourceItem("<", "Score is below threshold" ),
        new DataSourceItem("<=", "Score is below or equal threshold" )
    };

    IEnumerable<DataSourceItem> IStaticDataSourceItemHandler.GetData()
    {
        return ConditionList;
    }
}