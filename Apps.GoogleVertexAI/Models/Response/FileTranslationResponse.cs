using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Translate;

namespace Apps.GoogleVertexAI.Models.Response;
public class FileTranslationResponse : ITranslateFileOutput
{
    public FileReference File { get; set; }

    [Display("Total segments")]
    public int TotalSegmentsCount { get; set; }

    [Display("Translatable segments")]
    public int TotalTranslatable { get; set; }

    [Display("Targets updated")]
    public int TargetsUpdatedCount { get; set; }

    [Display("Processed batches")]
    public int ProcessedBatchesCount { get; set; }
}
