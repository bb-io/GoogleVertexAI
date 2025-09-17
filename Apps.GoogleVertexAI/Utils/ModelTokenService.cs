namespace Apps.GoogleVertexAI.Utils;

public static class ModelTokenService
{
    public static int GetMaxTokensForModel(string? modelName)
    {
        return modelName switch
        {
            "gemini-2.5-pro" => 65535,
            "gemini-2.5-flash" => 65535,
            "gemini-2.5-flash-lite" => 65535,
            "gemini-2.0-flash" => 8191,
            "gemini-2.0-flash-lite" => 8191,
            _ => 4096
        };
    }
}
