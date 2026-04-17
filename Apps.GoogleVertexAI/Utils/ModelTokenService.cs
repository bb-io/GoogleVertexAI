namespace Apps.GoogleVertexAI.Utils;

public static class ModelTokenService
{
    public static int GetMaxTokensForModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return 4096;
        }

        if (modelName.StartsWith("gemini-3.", StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith("gemini-3-", StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith("gemini-2.5-", StringComparison.OrdinalIgnoreCase))
        {
            return 65535;
        }

        if (modelName.StartsWith("gemini-2.0-flash", StringComparison.OrdinalIgnoreCase))
        {
            return 8191;
        }

        return 4096;
    }
}
