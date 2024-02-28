using Blackbird.Applications.Sdk.Common;

namespace Apps.GoogleVertexAI;

public class VertexAiApplication : IApplication
{
    public string Name
    {
        get => "Google Vertex AI";
        set { }
    }

    public T GetInstance<T>()
    {
        throw new NotImplementedException();
    }
}