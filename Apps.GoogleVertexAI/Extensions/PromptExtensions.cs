namespace Apps.GoogleVertexAI.Extensions;

public static class PromptExtensions
{
    public static string FromBlackbirdPrompt(this string inputPrompt)
    {
        var promptSegments = inputPrompt.Split(";;");

        if (promptSegments.Length == 1)
            return promptSegments[0];

        if (promptSegments.Length == 2 || promptSegments.Length == 3)
            return $"{promptSegments[0]}\n\n{promptSegments[1]}";

        throw new("Wrong blackbird prompt format");
    }
}