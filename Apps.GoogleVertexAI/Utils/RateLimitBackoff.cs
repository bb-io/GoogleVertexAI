namespace Apps.GoogleVertexAI.Utils;

// Shared delays for the 429 / RESOURCE_EXHAUSTED retries of both connections.
public static class RateLimitBackoff
{
    public const int RetryCount = 8;

    private const int BaseBackoffSeconds = 1;
    private const int MaxBackoffSeconds = 16;
    private const int MaxServerDelaySeconds = 60;

    private static readonly Random Jitter = new();

    public static TimeSpan GetDelay(int retryAttempt, int? serverDelaySeconds = null)
    {
        // Prefer the delay the server asked for (Retry-After or Gemini's RetryInfo).
        if (serverDelaySeconds is > 0)
        {
            return TimeSpan.FromSeconds(Math.Min(serverDelaySeconds.Value, MaxServerDelaySeconds));
        }

        // Otherwise exponential backoff (1s, 2s, 4s, 8s, 16s...), capped, so the burst window clears.
        var backoff = Math.Min(BaseBackoffSeconds * Math.Pow(2, retryAttempt - 1), MaxBackoffSeconds);

        // Jitter keeps parallel requests that were throttled together from retrying in lockstep.
        int jitterMilliseconds;
        lock (Jitter)
        {
            jitterMilliseconds = Jitter.Next(0, 500);
        }

        return TimeSpan.FromSeconds(backoff) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }
}
