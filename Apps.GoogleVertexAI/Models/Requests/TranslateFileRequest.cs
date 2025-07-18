﻿using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Blueprints.Handlers;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Translate;

namespace Apps.GoogleVertexAI.Models.Requests;
public class TranslateFileRequest : ITranslateFileInput
{
    public FileReference File { get; set; }

    [Display("Source language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string? SourceLanguage { get; set; }

    [Display("Target language")]
    [StaticDataSource(typeof(LocaleDataSourceHandler))]
    public string TargetLanguage { get; set; }

    [StaticDataSource(typeof(AIModelDataSourceHandler))]
    [Display("Model")]
    public required string AIModel { get; set; }

    [Display("Output file handling", Description = "Determine the format of the output file. The default Blackbird behavior is to convert to XLIFF for future steps."), StaticDataSource(typeof(ProcessFileFormatHandler))]
    public string? OutputFileHandling { get; set; }
}
