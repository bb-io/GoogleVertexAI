using Blackbird.Applications.Sdk.Utils.Sdk.DataSourceHandlers;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryDataSourceHandler : EnumDataHandler
{
    protected override Dictionary<string, string> EnumValues => new()
    {
        { "SexuallyExplicit", "Sexually explicit" },
        { "HateSpeech", "Hate speech" },
        { "Harassment", "Harassment" },
        { "DangerousContent", "Dangerous content" }
    };
}