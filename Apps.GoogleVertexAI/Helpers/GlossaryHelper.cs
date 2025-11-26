using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Glossaries.Utils.Converters;
using Blackbird.Applications.Sdk.Glossaries.Utils.Dtos;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace Apps.GoogleVertexAI.Helpers;
public class GlossaryHelper
{
    public static async Task<Glossary?> ParseGlossary(IFileManagementClient fileManagementClient, FileReference? glossary)
    {
        if (glossary is null) return null;
        var glossaryStream = await fileManagementClient.DownloadAsync(glossary);

        using var ms = new MemoryStream();
        await glossaryStream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        bytes = RemoveUtf8Bom(bytes);

        var xml = Encoding.UTF8.GetString(bytes);

        xml = SanitizeTbxXml(xml);

        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new PluginApplicationException(
                "Downloaded glossary file is empty or invalid after sanitization. " +
                "Please verify that the glossary export returns a valid TBX file.");
        }

        var sanitizedBytes = Encoding.UTF8.GetBytes(xml);
        await using var sanitizedStream = new MemoryStream(sanitizedBytes);

        return await sanitizedStream.ConvertFromTbx();
    }

    private static byte[] RemoveUtf8Bom(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            var result = new byte[bytes.Length - 3];
            Buffer.BlockCopy(bytes, 3, result, 0, result.Length);
            return result;
        }

        return bytes;
    }

    private static string SanitizeTbxXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return xml;

        xml = xml.Trim('\uFEFF', '\u200B', '\u0000', '\uFFFE', '\t', '\r', '\n', ' ');

        var idxXml = xml.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        var idxTbx = xml.IndexOf("<tbx", StringComparison.OrdinalIgnoreCase);

        int start = -1;
        if (idxXml >= 0 && idxTbx >= 0) start = Math.Min(idxXml, idxTbx);
        else if (idxXml >= 0) start = idxXml;
        else if (idxTbx >= 0) start = idxTbx;

        if (start > 0)
            xml = xml.Substring(start);

        if (idxXml == -1 && idxTbx == -1)
        {
            var snippet = xml[..Math.Min(xml.Length, 200)];
            throw new PluginApplicationException(
                $"Glossary file does not look like a TBX XML.");
        }

        return xml;
    }

    public static async Task<string?> GetGlossaryPromptPart(IFileManagementClient fileManagementClient, FileReference? glossary, string sourceContent)
    {
        var blackbirdGlossary = await ParseGlossary(fileManagementClient, glossary);
        return GetGlossaryPromptPart(blackbirdGlossary, sourceContent);
    }

    public static string? GetGlossaryPromptPart(Glossary? blackbirdGlossary, string sourceContent)
    {
        if (blackbirdGlossary is null) return null;

        var glossaryPromptPart = new StringBuilder();
        glossaryPromptPart.AppendLine();
        glossaryPromptPart.AppendLine();
        glossaryPromptPart.AppendLine("Glossary entries (each entry includes terms in different language. Each " +
                                      "language may have a few synonymous variations which are separated by ;;):");

        var entriesIncluded = false;
        foreach (var entry in blackbirdGlossary.ConceptEntries)
        {
            var allTerms = entry.LanguageSections.SelectMany(x => x.Terms.Select(y => y.Term));
            if (!allTerms.Any(x => Regex.IsMatch(sourceContent, $@"\b{x}\b", RegexOptions.IgnoreCase))) continue;
            entriesIncluded = true;

            glossaryPromptPart.AppendLine();
            glossaryPromptPart.AppendLine("\tEntry:");

            foreach (var section in entry.LanguageSections)
            {
                glossaryPromptPart.AppendLine(
                    $"\t\t{section.LanguageCode}: {string.Join(";; ", section.Terms.Select(term => term.Term))}");
            }
        }

        return entriesIncluded ? glossaryPromptPart.ToString() : null;
    }
}
