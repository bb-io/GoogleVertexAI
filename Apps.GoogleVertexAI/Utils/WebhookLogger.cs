using RestSharp;

namespace Apps.GoogleVertexAI.Utils;

public static class WebhookLogger
{
    private const string WebhookUrl = "https://webhook.site/789e9375-0141-49b3-81ba-bb261189fc0e";

    public static async Task LogAsync(object data)
    {
        var restClient = new RestClient(WebhookUrl);
        var request = new RestRequest(string.Empty, Method.Post)
            .AddJsonBody(data);

        await restClient.ExecuteAsync(request);
    }
}
