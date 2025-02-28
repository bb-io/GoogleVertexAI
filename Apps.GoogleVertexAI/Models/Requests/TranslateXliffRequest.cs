using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class TranslateXliffRequest
{
    public FileReference File { get; set; }

    [DataSource(typeof(AIModelDataSourceHandler))]
    [Display("AI model used")]
    public required string AIModel { get; set; }
}