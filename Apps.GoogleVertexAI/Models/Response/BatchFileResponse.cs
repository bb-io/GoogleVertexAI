using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class BatchFileResponse
{
    [Display("Tranlated file")]
    public FileReference File { get; set; } = new();
    public UsageDto Usage { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    [Display("Total segments count")]
    public int TotalSegmentsCount { get; set; }
    
    [Display("Processed segments count")]
    public int ProcessedSegmentsCount { get; set; }
    
    [Display("Updated segments count")]
    public int UpdatedSegmentsCount { get; set; }
}