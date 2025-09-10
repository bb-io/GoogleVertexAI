using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.Models.Requests;

public class AIModelRequest
{
    [Display("Model")]
    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    public required string AIModel { get; set; }
}
