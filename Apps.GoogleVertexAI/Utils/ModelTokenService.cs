namespace Apps.GoogleVertexAI.Utils;

public static class ModelTokenService
{
    public static int GetMaxTokensForModel(string? modelName)
    {
        return modelName switch
        {
            "gemini-2.5-pro" => 65536,
            "gemini-2.5-flash" => 65536,
            "gemini-2.5-flash-lite" => 65536,
            "gemini-2.0-flash" => 8192,
            "gemini-2.0-flash-lite" => 8192,
            _ => 4096
        };
    }
}
