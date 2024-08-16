using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ScoreXliffRequest
{
    public FileReference File { get; set; }

    [Display("Source Language")]
    public string? SourceLanguage { get; set; }

    [Display("Target Language")]
    public string? TargetLanguage { get; set; }

    public float? Threshold { get; set; }

    [StaticDataSource(typeof(ConditionDataSourceHandler))]
    public string? Condition { get; set; }

    [Display("New Target State")]
    [StaticDataSource(typeof(XliffStateDataSourceHandler))]
    public string? State { get; set; }
}