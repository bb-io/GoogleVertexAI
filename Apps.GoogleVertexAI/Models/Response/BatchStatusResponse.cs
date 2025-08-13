using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response
{
    public class BatchStatusResponse
    {
        public string State { get; set; }

        [Display("Successful unit count")]
        public long SuccessfulCount { get; set; }

        [Display("Failed unit count")]
        public long FailedCount { get; set; }

        [Display("Output URI prefix")]
        public string? OutputUriPrefix { get; set; }

        [Display("Error code")]
        public string? ErrorCode { get; set; }

        [Display("Error message")]
        public string? ErrorMessage { get; set; }

        [Display("Partial failures")]
        public List<string>? PartialFailures { get; set; }
    }
}
