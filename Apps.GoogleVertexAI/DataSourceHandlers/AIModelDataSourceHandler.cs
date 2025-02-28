using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers
{
    public class AIModelDataSourceHandler : IStaticDataSourceItemHandler
    {
        public IEnumerable<DataSourceItem> GetData()
        {
            return new List<DataSourceItem> {
                new DataSourceItem("gemini-1.5-pro-002","Gemini 1.5 Pro") ,
                new DataSourceItem("gemini-1.0-pro","Gemini 1.0 Pro")
            };
        }
    }
}
