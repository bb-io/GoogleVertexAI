using Blackbird.Applications.Sdk.Utils.Sdk.DataSourceHandlers;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryThresholdDataSourceHandler : EnumDataHandler
{
    protected override Dictionary<string, string> EnumValues => new()
    {
        { "BLOCK_NONE", "Block none" },
        { "BLOCK_LOW_AND_ABOVE", "Block low and above" },
        { "BLOCK_MEDIUM_AND_ABOVE", "Block medium and above" },
        { "BLOCK_ONLY_HIGH", "Block only high" }
    };
}