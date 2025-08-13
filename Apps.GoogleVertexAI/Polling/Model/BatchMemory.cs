namespace Apps.GoogleVertexAI.Polling.Model
{
    public class BatchMemory
    {
        public DateTime LastPollingTime { get; set; }
        public bool Triggered { get; set; }
    }
}
