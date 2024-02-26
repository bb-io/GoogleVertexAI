namespace Apps.GoogleVertexAI.Models.Response.Gemini;

public record GenerateTextResponse(IEnumerable<Candidate> Candidates);

public record Candidate(Content Content);

public record Content(IEnumerable<Part> Parts);

public record Part(string Text);