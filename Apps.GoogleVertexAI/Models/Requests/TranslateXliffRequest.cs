using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class TranslateXliffRequest
{
    public FileReference File { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("Model")]
    public required string AIModel { get; set; }

    [Display("Process only segments with this state", Description = "Only translation units with this value in the target state will be processed")]
    [StaticDataSource(typeof(XliffStateDataSourceHandler))]
    public string? ProcessOnlyTargetState { get; set; }

    [Display("Update locked segments", Description = "By default it set to false. If false, OpenAI will not change the segments that are locked in the XLIFF file.")]
    public bool? PostEditLockedSegments { get; set; }
}