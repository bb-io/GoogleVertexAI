namespace Apps.GoogleVertexAI.Extensions;

public static class StringExtensions
{
    public static string ToXliffFileName(this string fileName)
    {
        return Path.ChangeExtension(fileName, ".xliff");
    }
}