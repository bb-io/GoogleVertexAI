using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class StartBatchResponse
{
    [Display("Batch job name")]
    public string JobName { get; set; }

    [Display("Transformation file")]
    public FileReference TransformationFile { get; set; }

    [Display("Total segments")]
    public int TotalSegmentsCount { get; set; }

    [Display("Segments to process")]
    public int SegmentsToProcessCount { get; set; }

    [Display("Requests in batch")]
    public int RequestsCount { get; set; }

    [Display("System prompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Display("Usage", Description = "Tokens used before the batch was submitted, for language detection and the document summary.")]
    public UsageDto Usage { get; set; } = new();
}
