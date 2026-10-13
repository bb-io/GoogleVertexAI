using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ExtractLocalizableContentFromImageRequest
{
    public required FileReference Image { get; set; }

    [Display("Source language", Description = "Optional source language. If omitted, Gemini detects it from the image.")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string? SourceLanguage { get; set; }

    [Display("Output file name", Description = "The name of the output XLIFF file without the extension.")]
    public string? OutputFileName { get; set; }
}
