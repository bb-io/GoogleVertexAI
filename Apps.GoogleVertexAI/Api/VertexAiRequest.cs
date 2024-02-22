using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;

namespace Apps.GoogleVertexAI.Api;

public class VertexAiRequest : RestRequest
{
    public VertexAiRequest(string endpoint, Method method) : base(endpoint, method)
    {
    }

    public VertexAiRequest WithJsonBody(object requestBody)
        => (VertexAiRequest)this.WithJsonBody(requestBody, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
}