using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Extensions;

public static class StringExtensions
{
    public static string ToXliffFileName(this string fileName)
    {
        return Path.ChangeExtension(fileName, ".xliff");
    }
    
    public static string StripTags(this string input)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(input);
        return Regex.Replace(decoded, @"\{\d+>|<\d+[}>]", "");
    }
}