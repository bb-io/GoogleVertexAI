namespace Apps.GoogleVertexAI.Helpers;

public static class XmlHelpers
{
    public static string EnsureXmlSafe(string content)
    {
        if (string.IsNullOrEmpty(content) || IsWellFormedFragment(content))
            return content;

        return content
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
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