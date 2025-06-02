using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class AIModelWithFileDataSourceHandler : IStaticDataSourceItemHandler
{
    private static readonly Dictionary<string, string> Data = new()
    {
        {"gemini-2.5-flash-preview-04-17","Gemini 2.5 Flash Preview 04-17" },
        {"gemini-2.5-pro-exp-03-25","Gemini 2.5 Pro Experimental 03-25" },
        {"gemini-2.5-pro-preview-03-25","Gemini 2.5 Pro Preview 03-25" },
        {"gemini-2.0-flash","Gemini 2.0 Flash" },
        {"gemini-2.0-flash-lite","Gemini 2.0 Flash-Lite" },
        {"gemini-2.0-pro-exp-02-05", " Gemini 2.0 Pro Experimental 02-05" },
        {"gemini-1.5-flash","Gemini 1.5 Flash"},
        {"gemini-1.0-pro-vision","Gemini 1.0 Pro Vision"}
    };

    public IEnumerable<DataSourceItem> GetData()
    {
        return Data.Select(x => new DataSourceItem(x.Key, x.Value));
    }
}