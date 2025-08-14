using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Response
{
    public class StartBatchXliffResponse
    {
        [Display("Job name")]
        public string JobName { get; set; }

        [Display("Input URI")]
        public string InputUri { get; set; }

        [Display("Output URI prefix")]
        public string OutputUriPrefix { get; set; }

        [Display("Items")]
        public int Items { get; set; }
    }
}
