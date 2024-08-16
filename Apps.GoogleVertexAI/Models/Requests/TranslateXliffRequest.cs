using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class TranslateXliffRequest
{
    public FileReference File { get; set; }
    
    [Display("Update locked segments", Description = "By default it set to false. If true, OpenAI will not change the segments that are locked in the XLIFF file.")]
    public bool? UpdateLockedSegments { get; set; }
}