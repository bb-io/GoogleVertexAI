﻿using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static
{
    public class AIModelDataSourceHandler : IStaticDataSourceItemHandler
    {
        private static Dictionary<string, string> Data = new Dictionary<string, string> {
                {"gemini-1.5-pro","Gemini 1.5 Pro" } ,
                {"gemini-1.0-pro","Gemini 1.0 Pro" }
            };
        public IEnumerable<DataSourceItem> GetData()
        {
            return Data.Select(x => new DataSourceItem(x.Key, x.Value));
        }
    }
}
