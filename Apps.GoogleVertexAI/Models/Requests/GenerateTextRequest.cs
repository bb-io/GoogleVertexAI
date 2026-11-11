using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GenerateTextRequest : PromptRequest
{
    public string Prompt { get; set; } = string.Empty;

    [Display("Is Blackbird prompt", Description = "Parameter indicating whether the input prompt is the output " +
                                                  "of one of the AI Utilities app's actions; defaults to 'False'.")]
    public bool? IsBlackbirdPrompt { get; set; }

    [Display("Model")]
    [DataSource(typeof(AIModelDynamicDataSourceHandler))]
    public required string AIModel { get; set; }

    [Display("File search store names", Description = "Optional. When provided, Gemini File Search will be used to ground the response against the specified stores."), DataSource(typeof(FileSearchStoreDataSourceHandler))]
    public IEnumerable<string>? FileSearchStoreNames { get; set; }

    [Display("Metadata filter", Description = "Optional AIP-160 filter expression used to restrict File Search results, for example: author = \"Robert Graves\".")]
    public string? MetadataFilter { get; set; }

    [Display("YouTube video URL", Description = "Optional. Link to a public YouTube video, for example https://www.youtube.com/watch?v=VIDEO_ID. " +
                                                "The video is passed to Gemini as media input, so the model analyzes both its audio and its visuals.")]
    public string? YouTubeVideoUrl { get; set; }
}
