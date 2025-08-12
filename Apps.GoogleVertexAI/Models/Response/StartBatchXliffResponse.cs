namespace Apps.GoogleVertexAI.Models.Response
{
    public class StartBatchXliffResponse
    {
        public string JobName { get; set; }
        public string InputUri { get; set; }
        public string OutputUriPrefix { get; set; }
        public int Items { get; set; }
    }
}
