using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response
{
    public class GetMQMResponse
    {
        [Display("MQM report")]
        public string Report { get; set; }
        public UsageDto Usage { get; set; } = new();

        [Display("System prompt used")]
        public string SystemPrompt { get; set; }
    }
}
