using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI.Models.Requests;

public class BucketRequest
{
    [Display("Bucket size")]
    public int? BucketSize { get; set; }

    public int GetBucketSizeOrDefault() => BucketSize ?? 25;
}