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
        return await glossaryStream.ConvertFromTbx();
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
