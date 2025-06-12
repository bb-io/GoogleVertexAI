using RestSharp;

namespace Apps.GoogleVertexAI.Utils;

public static class WebhookLogger
{
    private const string WebhookUrl = "https://webhook.site/4eb4635b-96bb-4474-b68e-bf23ec240bc5";

    public static async Task LogAsync(object data)
    {
        var restClient = new RestClient(WebhookUrl);
        var request = new RestRequest(string.Empty, Method.Post)
            .AddJsonBody(data);

        await restClient.ExecuteAsync(request);
    }
}
