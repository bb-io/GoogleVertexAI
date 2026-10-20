using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Handlers;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GetBatchResultRequest
{
    [Display("Transformation file")]
    public FileReference OriginalXliff { get; set; }

    [Display("Output file handling", Description = "Determine the format of the output file. Default is the format chosen when the background job was started, otherwise XLIFF."), StaticDataSource(typeof(ProcessFileFormatHandler))]
    public string? OutputFileHandling { get; set; }
}
