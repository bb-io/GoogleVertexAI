using Blackbird.Ai.Files.Models;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Filters.Constants;

namespace Apps.GoogleVertexAI.DataSourceHandlers.Static;

public class GeminiProcessFileFormatHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData() =>
    [
        new(MediaTypes.Xliff2, "Interoperable XLIFF (default)"),
        new(OutputFileHandling.Xliff1, "XLIFF 1.2"),
        new(OutputFileHandling.Original, "Original data format")
    ];
}