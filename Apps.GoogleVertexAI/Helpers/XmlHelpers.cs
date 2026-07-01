using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Helpers;

public static class XmlHelpers
{
    public static string EnsureXmlSafe(string content)
    {
        if (string.IsNullOrEmpty(content) || IsWellFormedFragment(content))
            return content;

        content = Regex.Replace(content, @"&(?!#\d+;|#x[0-9a-fA-F]+;|[A-Za-z][A-Za-z0-9]*;)", "&amp;");
        content = Regex.Replace(content, "<(?![A-Za-z_/!?])", "&lt;");
        return content;
    }

    private static bool IsWellFormedFragment(string content)
    {
        try
        {
            _ = System.Xml.Linq.XDocument.Parse($"<x>{content}</x>");
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }
}