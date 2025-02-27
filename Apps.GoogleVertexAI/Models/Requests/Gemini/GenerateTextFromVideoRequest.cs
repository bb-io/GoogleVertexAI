using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests.Gemini;

public class GenerateTextFromVideoRequest : GenerateTextRequest
{  
    [Display("Video", Description = "Video in any of the following formats: MOV, MPEG, MP4, MPG, AVI, WMV, " +
                                    "MPEGPS, FLV, with a size limit of 20 MB. Video cannot be added if an " +
                                    "image is already included.")]
    public FileReference Video { get; set; }
}