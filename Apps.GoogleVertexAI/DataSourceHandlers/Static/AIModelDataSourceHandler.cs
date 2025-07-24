using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class AIModelDataSourceHandler : IStaticDataSourceItemHandler
{
    private static readonly Dictionary<string, string> Data = new()
    {
        { "gemini-2.5-pro","Gemini 2.5 Pro" },
        { "gemini-2.5-flash","Gemini 2.5 Flash" },
        { "gemini-2.0-flash","Gemini 2.0 Flash" },
        { "gemini-2.0-flash-lite","Gemini 2.0 Flash-Lite" },
        { "gemini-1.5-flash","Gemini 1.5 Flash" },
        { "gemini-1.5-pro","Gemini 1.5 Pro" },
    };
    
    public IEnumerable<DataSourceItem> GetData()
    {
        return Data.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}
