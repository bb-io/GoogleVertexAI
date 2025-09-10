using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GetTranslationIssuesRequest
{
    public FileReference File { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("AI model to use")]
    public required string AIModel { get; set; }

    [Display("Source language", Description = "Override the source language specified in the XLIFF file")]
    public string? SourceLanguage { get; set; }

    [Display("Target language", Description = "Override the target language specified in the XLIFF file")]
    public string? TargetLanguage { get; set; }

    [Display("Target audience", Description = "Specify the target audience for the translation")]
    public string? TargetAudience { get; set; }

    [Display("Include finalized segments", Description = "By default it set to false. If false, the LLM will not process the segments that are locked or set to final.")]
    public bool? PostEditLockedSegments { get; set; }

}