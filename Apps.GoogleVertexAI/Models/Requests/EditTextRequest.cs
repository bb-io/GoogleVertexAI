using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Edit;

namespace Apps.GoogleVertexAI.Models.Requests;
public class EditTextRequest : IEditTextInput
{
    [Display("Source text")]
    public string SourceText { get; set; }

    [Display("Target text")]
    public string TargetText { get; set; }

    [Display("Source language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string TargetLanguage { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("Model")]
    public required string AIModel { get; set; }

}
