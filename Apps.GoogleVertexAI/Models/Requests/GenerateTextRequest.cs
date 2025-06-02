using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GenerateTextRequest : PromptRequest
{
    public string Prompt { get; set; }

    [Display("Is Blackbird prompt", Description = "Parameter indicating whether the input prompt is the output " +
                                                  "of one of the AI Utilities app's actions; defaults to 'False'.")]
    public bool? IsBlackbirdPrompt { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("AI model used")]
    public required string AIModel { get; set; }
}