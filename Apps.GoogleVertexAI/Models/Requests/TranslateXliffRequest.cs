using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class TranslateXliffRequest
{
    public FileReference File { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("AI model used")]
    public required string AIModel { get; set; }
}