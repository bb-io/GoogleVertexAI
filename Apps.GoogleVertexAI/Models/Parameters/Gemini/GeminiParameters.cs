namespace Apps.GoogleVertexAI.Models.Parameters.Gemini;

public record GeminiParameters
{
    public GeminiParameters(IEnumerable<PromptData> data, GenerationConfiguration generationConfiguration, 
        IEnumerable<SafetySetting>? safetySettings = null)
    {
        Contents = new[] { new Content("USER", data.Where(d => d.Text != null || d.InlineData != null ))};
        GenerationConfig = generationConfiguration;
        SafetySettings = safetySettings;
    }
    
    public IEnumerable<Content> Contents { get; }
    public GenerationConfiguration GenerationConfig { get; }
    public IEnumerable<SafetySetting>? SafetySettings { get; }
}

#region Contents

public record Content(string Role, IEnumerable<PromptData> Parts);

public record PromptData
{
    public PromptData(string text)
    {
        Text = text;
    }

    public PromptData(InlineData? inlineData)
    {
        InlineData = inlineData;
    }
    
    public string? Text { get; }
    
    public InlineData? InlineData { get; }
}
 
public record InlineData(string MimeType, string Data); // for media; data is the base64 encoding of the image or video to include inline in the prompt

#endregion

#region Generation config

public record GenerationConfiguration(
    int? MaxOutputTokens = null, 
    float? Temperature = null, 
    float? TopP = null,
    int? TopK = null);

#endregion

#region Safety settings

public record SafetySetting(string Category, string Threshold);

#endregion