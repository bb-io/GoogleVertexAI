using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GoogleVertexAI.DataSourceHandlers;

public class ImageGenerationModelDataSourceHandler(InvocationContext invocationContext)
    : AIModelDynamicDataSourceHandler(invocationContext)
{
    protected override bool IsSupportedModel(string modelId, bool hideLatestAndRobotics)
        => IsSupportedImageGenerationModel(modelId);

    protected override string GetDeduplicationKey(string modelId, string displayName)
        => displayName;

    internal static bool IsSupportedImageGenerationModel(string modelId)
        => modelId.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase)
           && modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
           && !modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase)
           && !modelId.Contains("live", StringComparison.OrdinalIgnoreCase)
           && !modelId.Contains("tts", StringComparison.OrdinalIgnoreCase);
}
