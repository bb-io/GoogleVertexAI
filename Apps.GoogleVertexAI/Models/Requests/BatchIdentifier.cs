using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Requests
{
    public class BatchIdentifier
    {
        [Display("Batch job name")]
        public string JobName { get; set; } = default!;
    }
}
