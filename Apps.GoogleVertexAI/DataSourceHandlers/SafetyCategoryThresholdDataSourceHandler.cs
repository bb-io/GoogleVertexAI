using Blackbird.Applications.Sdk.Utils.Sdk.DataSourceHandlers;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryThresholdDataSourceHandler : EnumDataHandler
{
    protected override Dictionary<string, string> EnumValues => new()
    {
        { "BlockNone", "Block none" },
        { "BlockLowAndAbove", "Block low and above" },
        { "BlockMediumAndAbove", "Block medium and above" },
        { "BlockOnlyHigh", "Block only high" }
    };
}