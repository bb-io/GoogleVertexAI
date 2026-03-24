namespace Apps.GoogleVertexAI.Constants;

public static class ConnectionTypes
{
    public const string ServiceAccount = "ServiceAccount";
    public const string GeminiApiKey = "GeminiApiKey";

    public static readonly HashSet<string> SupportedConnectionTypes =
    [
        ServiceAccount,
        GeminiApiKey
    ];
}
