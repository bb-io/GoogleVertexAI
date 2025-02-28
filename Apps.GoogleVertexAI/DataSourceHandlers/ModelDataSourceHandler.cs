using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.GoogleVertexAI.DataSourceHandlers
{
    internal class ModelDataSourceHandler : IStaticDataSourceItemHandler
    {
        public IEnumerable<DataSourceItem> GetData()
        {
            return new List<DataSourceItem> {
                new DataSourceItem("gemini-2.0-flash-001","Gemini 2.0 Flash") ,
                new DataSourceItem("gemini-2.0-flash-lite","Gemini 2.0 Flash-Lite") ,
                new DataSourceItem("gemini-1.5-flash-002","Gemini 1.5 Flash") ,
                new DataSourceItem("gemini-1.5-pro-002","Gemini 1.5 Pro") ,
                new DataSourceItem("gemini-1.0-pro-vision-001","Gemini 1.0 Pro Vision") ,
                new DataSourceItem("gemini-1.0-pro","Gemini 1.0 Pro")
            };
        }
    }
}
