using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests;

public class ShortenContentRequest
{
    public FileReference File { get; set; } = new();

    [DataSource(typeof(AIModelDynamicDataSourceHandler))]
    [Display("Model")]
    public required string AIModel { get; set; }

    [Display("Segment states", Description = "Only units where all non-ignorable segments have one of these states will be processed. Defaults to initial and translated.")]
    [StaticDataSource(typeof(XliffV2StateDataSourceHandler))]
    public IEnumerable<string>? SegmentStates { get; set; }

    [Display("Batch size", Description = "Number of units to send to Gemini at once. Default value: 1500.")]
    public int? BatchSize { get; set; }

    [Display("Retries", Description = "Number of retries for units where Gemini still returns target text over the size restriction. Default value: 3.")]
    public int? RetryCount { get; set; }
}
