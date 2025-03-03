using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static
{
    public class AIModelWithFileDataSourceHandler : IStaticDataSourceItemHandler
    {
        private static Dictionary<string, string> Data = new Dictionary<string, string> {
                { "gemini-2.0-flash-001","Gemini 2.0 Flash" } ,
                {"gemini-2.0-flash-lite","Gemini 2.0 Flash-Lite" },
                {"gemini-1.5-flash-002","Gemini 1.5 Flash"},
                {"gemini-1.0-pro-vision-001","Gemini 1.0 Pro Vision"}
            };

        public IEnumerable<DataSourceItem> GetData()
        {
            return Data.Select(x => new DataSourceItem(x.Key, x.Value));
        }
    }
}
