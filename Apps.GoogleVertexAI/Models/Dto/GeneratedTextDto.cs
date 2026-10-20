namespace Apps.GoogleVertexAI.Models.Dto;

// FinishReason is Gemini's own value, for example "STOP" or "MAX_TOKENS".
public record GeneratedTextDto(string Text, UsageDto Usage, string? FinishReason);
