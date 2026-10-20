using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Requests;

public class BucketRequest
{
    [Display("Bucket size", Description = "Specify the maximum number of source texts to be processed in one request of the batch. Default value: 250. Buckets are also sized to fit the model's output limit. (See our documentation for an explanation)")]
    public int? BucketSize { get; set; }
}
