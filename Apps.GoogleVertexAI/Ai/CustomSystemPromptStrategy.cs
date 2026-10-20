using Blackbird.Ai.Llm.Models;
using Blackbird.Ai.Parsing.Models;
using Blackbird.Ai.Prompts.Abstract;
using Blackbird.Ai.Prompts.Models;

namespace Apps.GoogleVertexAI.Ai;

// A "System prompt" input must not reach the library's SystemPrompt option, which replaces the whole system
// prompt, response contract included. The user's text is put in front of the library prompt instead.
public sealed class CustomSystemPromptStrategy(string systemPrompt, IPromptStrategy inner) : IPromptStrategy
{
    public JsonSchemaSpec? ResponseSchema => inner.ResponseSchema;

    public string BuildSystemPrompt(PromptContext context) =>
        $"{systemPrompt.Trim()}{Environment.NewLine}{Environment.NewLine}{inner.BuildSystemPrompt(context)}";

    public string BuildUserPrompt(PromptContext context, IReadOnlyList<PromptItem> items) =>
        inner.BuildUserPrompt(context, items);

    public LlmParseResult ParseResponse(string? raw) => inner.ParseResponse(raw);

    public static IPromptStrategy? For(string? systemPrompt, Func<IPromptStrategy> inner) =>
        string.IsNullOrWhiteSpace(systemPrompt) ? null : new CustomSystemPromptStrategy(systemPrompt, inner());
}
