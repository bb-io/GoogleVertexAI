using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Response
{
    public class StartBatchResponse
    {
        [Display("Batch job name")]
        public string JobName { get; set; }

        [Display("Transformation file")]
        public FileReference TransformationFile { get; set; }
    }
}
