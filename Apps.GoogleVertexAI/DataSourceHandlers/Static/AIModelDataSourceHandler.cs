using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class AIModelDataSourceHandler : IStaticDataSourceItemHandler
{
    private static readonly Dictionary<string, string> Data = new()
    {
        { "gemini-3-pro-preview", "Gemini 3 Pro" },
        { "gemini-2.5-pro","Gemini 2.5 Pro" },
        { "gemini-2.5-flash","Gemini 2.5 Flash" },
        { "gemini-2.5-flash-lite","Gemini 2.5 Flash-Lite" },
        { "gemini-2.0-flash","Gemini 2.0 Flash" },
        { "gemini-2.0-flash-lite","Gemini 2.0 Flash-Lite" },
    };
    
    public IEnumerable<DataSourceItem> GetData()
    {
        return Data.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}
