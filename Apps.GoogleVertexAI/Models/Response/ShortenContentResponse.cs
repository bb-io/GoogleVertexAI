using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response;

public class ShortenContentResponse
{
    public FileReference File { get; set; } = new();

    [Display("Total units")]
    public int TotalUnitsCount { get; set; }

    [Display("Units with restriction")]
    public int UnitsWithRestrictionCount { get; set; }

    [Display("Units matching state filter")]
    public int UnitsMatchedFilterCount { get; set; }

    [Display("Units over limit")]
    public int UnitsOverLimitCount { get; set; }

    [Display("Units updated")]
    public int UnitsUpdatedCount { get; set; }

    [Display("Units remaining over limit")]
    public int UnitsRemainingOverLimitCount { get; set; }

    [Display("Processed batches")]
    public int ProcessedBatchesCount { get; set; }

    [Display("Retry attempts")]
    public int RetryAttemptsCount { get; set; }

    [Display("System prompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Display("Prompt template")]
    public string PromptTemplate { get; set; } = string.Empty;

    [Display("Usage")]
    public UsageDto Usage { get; set; } = new();

    [Display("Error messages")]
    public IEnumerable<string>? ErrorMessages { get; set; }
}
