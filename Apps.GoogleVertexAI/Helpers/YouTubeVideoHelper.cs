using Apps.GoogleVertexAI.Models.Dto;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Helpers;

public static class YouTubeVideoHelper
{
    private const string VideoMimeType = "video/mp4";
    private const string ShortLinkHost = "youtu.be";
    private const string YouTubeDomain = "youtube.com";

    public static string? NormalizeUrl(string? youTubeVideoUrl)
    {
        if (string.IsNullOrWhiteSpace(youTubeVideoUrl))
        {
            return null;
        }

        var trimmedUrl = youTubeVideoUrl.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) || !IsWebUrl(uri))
        {
            throw InvalidUrl(trimmedUrl);
        }

        if (uri.Host.Equals(ShortLinkHost, StringComparison.OrdinalIgnoreCase))
        {
            return ToWatchUrl(uri, trimmedUrl);
        }

        return IsYouTubeHost(uri.Host) ? trimmedUrl : throw InvalidUrl(trimmedUrl);
    }

    public static List<Part>? ToVertexParts(string? youTubeVideoUrl)
    {
        var videoUrl = NormalizeUrl(youTubeVideoUrl);
        return videoUrl is null
            ? null
            : [new Part { FileData = new FileData { FileUri = videoUrl, MimeType = VideoMimeType } }];
    }

    public static GeminiPart? ToGeminiPart(string? youTubeVideoUrl)
    {
        var videoUrl = NormalizeUrl(youTubeVideoUrl);
        return videoUrl is null
            ? null
            : new GeminiPart { FileData = new GeminiFileData { FileUri = videoUrl, MimeType = VideoMimeType } };
    }

    private static bool IsWebUrl(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;

    private static bool IsYouTubeHost(string host)
        => host.Equals(YouTubeDomain, StringComparison.OrdinalIgnoreCase)
           || host.EndsWith($".{YouTubeDomain}", StringComparison.OrdinalIgnoreCase);

    private static string ToWatchUrl(Uri shortLink, string originalUrl)
    {
        var videoId = shortLink.AbsolutePath.Trim('/');
        return string.IsNullOrEmpty(videoId) || videoId.Contains('/')
            ? throw InvalidUrl(originalUrl)
            : $"https://www.{YouTubeDomain}/watch?v={videoId}";
    }

    private static PluginMisconfigurationException InvalidUrl(string url)
        => new($"'{url}' is not a valid YouTube video URL. Provide a link to a public YouTube video, " +
               "for example https://www.youtube.com/watch?v=VIDEO_ID.");
}
