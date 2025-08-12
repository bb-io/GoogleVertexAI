using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response
{
    public class BatchJobStartResponse
    {
        public string JobId { get; set; }
        public string Status { get; set; }
        public FileReference OriginalFile { get; set; }
    }
}
