using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class ConditionDataSourceHandler : IStaticDataSourceItemHandler
{
    private static Dictionary<string,string> Data=> new()
    {
        { ">", "Score is above threshold"},
        {">=", "Score is above or equal threshold" },
        {"=", "Score is same as threshold" },
        {"<", "Score is below threshold" },
        {"<=", "Score is below or equal threshold" }
    };

    IEnumerable<DataSourceItem> IStaticDataSourceItemHandler.GetData()
    {
        return Data.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}