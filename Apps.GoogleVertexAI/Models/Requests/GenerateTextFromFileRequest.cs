using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GenerateTextFromFileRequest : PromptRequest
{
    public string Prompt { get; set; }

    [Display("Is Blackbird prompt", Description = "Parameter indicating whether the input prompt is the output " +
                                                  "of one of the AI Utilities app's actions; defaults to 'False'.")]
    public bool? IsBlackbirdPrompt { get; set; }

    [Display("File", Description = "Image in PNG or JPEG format with a size limit of 20 MB." +
                                    "Video in any of the following formats: MOV, MPEG, MP4, MPG, AVI, WMV, " +
                                    "MPEGPS, FLV, with a size limit of 20 MB.")]
    public FileReference File { get; set; }

    [DataSource(typeof(AIModelWithFileDataSourceHandler))]
    [Display("AI model used")]
    public required string AIModel { get; set; }

}