using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class GetTranslationIssuesRequest
{
    public FileReference File { get; set; }

    [DataSource(typeof(AIModelDataSourceHandler))]
    [Display("AI model used")]
    public required string AIModel { get; set; }

    [Display("Source language", Description = "Override the source language specified in the XLIFF file")]
    public string? SourceLanguage { get; set; }

    [Display("Target language", Description = "Override the target language specified in the XLIFF file")]
    public string? TargetLanguage { get; set; }

    [Display("Target audience", Description = "Specify the target audience for the translation")]
    public string? TargetAudience { get; set; }

    [Display("Process only segments with this state", Description = "Only translation units with this value in the target state will be processed")]
    [StaticDataSource(typeof(XliffStateDataSourceHandler))]
    public string? ProcessOnlyTargetState { get; set; }

    [Display("Update locked segments", Description = "By default it set to false. If false, OpenAI will not process the segments that are locked in the XLIFF file.")]
    public bool? PostEditLockedSegments { get; set; }
}