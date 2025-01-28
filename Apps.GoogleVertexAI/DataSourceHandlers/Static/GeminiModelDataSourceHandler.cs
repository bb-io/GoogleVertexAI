using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static
{
    public class GeminiModelDataSourceHandler : IStaticDataSourceHandler
    {
        private static Dictionary<string, string> ModelValues => new()
        {
            { "gemini-1.5-flash", "Gemini 1.5 Flash" },
            { "gemini-1.5-pro", "Gemini 1.5 Pro" }
        };

        public Dictionary<string, string> GetData()
        {
            return ModelValues;
        }
    }
}
