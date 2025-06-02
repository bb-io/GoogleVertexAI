using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryThresholdDataSourceHandler : IStaticDataSourceItemHandler
{
    private static Dictionary<string, string> EnumValues => new()
    {
        { "BlockNone", "Block none" },
        { "BlockLowAndAbove", "Block low and above" },
        { "BlockMediumAndAbove", "Block medium and above" },
        { "BlockOnlyHigh", "Block only high" }
    };

    public IEnumerable<DataSourceItem> GetData()
    {
        return EnumValues.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}