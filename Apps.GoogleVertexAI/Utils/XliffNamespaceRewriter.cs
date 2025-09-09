using System.Xml;
using System.Xml.Linq;

namespace Apps.GoogleVertexAI.Utils;

public static class XliffNamespaceRewriter
{
    /// <summary>
    /// Temporary solution to add the ITS namespace once per file until we add this to the Filters library.
    /// </summary>
    /// <param name="xliffInput">XLIFF string with ITS namespace declared per element.</param>
    /// <returns>XLIFF string with ITS declared once per file.</returns>
    public static string RewriteTargets(string xliffInput)
    {
        var doc = XDocument.Parse(xliffInput, LoadOptions.PreserveWhitespace);

        XNamespace itsNs = "http://www.w3.org/2005/11/its";

        // Add xmlns:itc once at the root if missing
        var root = doc.Root!;
        if (!root.Attributes().Any(a => a.IsNamespaceDeclaration && a.Name.LocalName == "itc"))
        {
            root.Add(new XAttribute(XNamespace.Xmlns + "itc", itsNs));
        }

        // Find all <target> elements
        var targets = root.Descendants().Where(e => e.Name.LocalName == "target");

        foreach (var target in targets)
        {
            // Remove any ITS namespace declarations for old prefixes on this element
            var nsDecls = target.Attributes()
                .Where(a => a.IsNamespaceDeclaration &&
                            a.Name.LocalName != "itc")
                .ToList();

            foreach (var nsDecl in nsDecls)
                nsDecl.Remove();

            // Pick only locQualityRatingScore regardless of namespace
            var scoreAttr = target.Attributes()
                .Where(a => !a.IsNamespaceDeclaration &&
                            a.Name.LocalName == "locQualityRatingScore")
                .FirstOrDefault();

            if (scoreAttr != null)
            {
                scoreAttr.Remove();
                var newAttr = new XAttribute(itsNs + scoreAttr.Name.LocalName, scoreAttr.Value);
                target.Add(newAttr);
            }
        }

        // Render output with explicit UTF-8 encoding
        var settings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false), // no BOM
            OmitXmlDeclaration = false,
            Indent = true,
            IndentChars = "  ",
            NewLineHandling = NewLineHandling.Replace,
        };

        using var sw = new System.IO.MemoryStream();
        using (var writer = XmlWriter.Create(sw, settings))
        {
            doc.Save(writer);
        }

        return System.Text.Encoding.UTF8.GetString(sw.ToArray());
    }
}