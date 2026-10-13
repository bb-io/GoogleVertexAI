using Apps.GoogleVertexAI.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ImageGenerationModelRequest
{
    [Display("Model")]
    [DataSource(typeof(ImageGenerationModelDataSourceHandler))]
    public required string AIModel { get; set; }
}
