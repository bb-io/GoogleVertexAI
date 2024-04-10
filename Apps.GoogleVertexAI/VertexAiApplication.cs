using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Metadata;

namespace Apps.GoogleVertexAI;

public class VertexAiApplication : IApplication, ICategoryProvider
{
    public IEnumerable<ApplicationCategory> Categories
    {
        get =>
        [
            ApplicationCategory.ArtificialIntelligence, ApplicationCategory.Multimedia, ApplicationCategory.GoogleApps
        ];
        set { }
    }

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