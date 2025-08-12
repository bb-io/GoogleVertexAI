namespace Apps.GoogleVertexAI.Models.Response
{
    public class BatchStatusResponse
    {
        public string State { get; set; } = default!;
        public long SuccessfulCount { get; set; }
        public long FailedCount { get; set; }
        public string? OutputUriPrefix { get; set; }

        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string>? PartialFailures { get; set; }
    }
}
