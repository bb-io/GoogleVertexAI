namespace Apps.GoogleVertexAI.Models.Parameters;

public record GeminiParameters
{
    public GeminiParameters(PromptData data, GenerationConfiguration generationConfiguration)
    {
        Contents = new[] { new Content("USER", new [] { data })};
        GenerationConfig = generationConfiguration;
    }
    
    public IEnumerable<Content> Contents { get; }
    public GenerationConfiguration GenerationConfig { get; }
}

public record Content(string Role, IEnumerable<PromptData> Parts);

public record PromptData(string Text);

public record GenerationConfiguration(int MaxOutputTokens);