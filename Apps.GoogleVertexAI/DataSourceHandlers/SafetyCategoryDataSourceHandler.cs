using Blackbird.Applications.Sdk.Utils.Sdk.DataSourceHandlers;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class SafetyCategoryDataSourceHandler : EnumDataHandler
{
    protected override Dictionary<string, string> EnumValues => new()
    {
        { "HARM_CATEGORY_SEXUALLY_EXPLICIT", "Sexually explicit" },
        { "HARM_CATEGORY_HATE_SPEECH", "Hate speech" },
        { "HARM_CATEGORY_HARASSMENT", "Harassment" },
        { "HARM_CATEGORY_DANGEROUS_CONTENT", "Dangerous content" }
    };
}