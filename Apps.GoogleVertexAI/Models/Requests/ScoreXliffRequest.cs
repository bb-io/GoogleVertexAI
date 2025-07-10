using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ScoreXliffRequest
{
    public FileReference File { get; set; }

    [Display("Source language")]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    public string? TargetLanguage { get; set; }

    public float? Threshold { get; set; }

    [StaticDataSource(typeof(ConditionDataSourceHandler))]
    public string? Condition { get; set; }

    [Display("New target state")]
    [StaticDataSource(typeof(XliffStateDataSourceHandler))]
    public string? State { get; set; }

    [DataSource(typeof(AIModelDataSourceHandler))]
    [Display("Model")]
    public required string AIModel { get; set; }
}