using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryDataSourceHandler : IStaticDataSourceItemHandler
{
    private static Dictionary<string, string> EnumValues => new()
    {
        { "SexuallyExplicit", "Sexually explicit" },
        { "HateSpeech", "Hate speech" },
        { "Harassment", "Harassment" },
        { "DangerousContent", "Dangerous content" }
    };

    public IEnumerable<DataSourceItem> GetData()
    {
        return EnumValues.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}