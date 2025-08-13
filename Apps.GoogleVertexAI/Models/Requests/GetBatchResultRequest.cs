using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GoogleVertexAI.Models.Requests
{
    public class GetBatchResultRequest
    {
        [Display("Original XLIFF file")]
        public FileReference OriginalXliff { get; set; }
    }
}
