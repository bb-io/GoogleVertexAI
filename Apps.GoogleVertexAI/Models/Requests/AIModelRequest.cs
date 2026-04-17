using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class AIModelRequest
{
    [Display("Model")]
    [DataSource(typeof(AIModelDynamicDataSourceHandler))]
    public required string AIModel { get; set; }
}
