using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests.Gemini;

public class GenerateTextRequest : PromptRequest
{
    public string Prompt { get; set; }
    
    [Display("Is Blackbird prompt", Description = "Parameter indicating whether the input prompt is the output " +
                                                  "of one of the AI Utilities app's actions; defaults to 'False'.")]
    public bool? IsBlackbirdPrompt { get; set; }
    
    [Display("Image", Description = "Image in PNG or JPEG format with a size limit of 20 MB. Cannot be added " +
                                    "if a video is already included.")]
    public FileReference? Image { get; set; }
    
    [Display("Video", Description = "Video in any of the following formats: MOV, MPEG, MP4, MPG, AVI, WMV, " +
                                    "MPEGPS, FLV, with a size limit of 20 MB. Video cannot be added if an " +
                                    "image is already included.")]
    public FileReference? Video { get; set; }
}