using System.Text.Json.Nodes;
using Apps.GoogleVertexAI.Helpers;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Filters.Transformations;
using GoogleVertexAI.Base;

namespace Tests.GoogleVertexAI;

[TestClass]
public class TranslationUnitsPromptBuilderTests
{
    private const string PhraseFileName = "tm-suggestions.mxliff";
    private const string FileWithoutAltTransName = "contentful_2.xlf";
    private const string SuggestionsPropertyName = "TranslationMemorySuggestions";

    [TestMethod]
    public async Task Build_with_tm_suggestions_adds_non_empty_phrase_tm_matches_to_their_segments()
    {
        var content = await LoadTransformation(PhraseFileName);

        var prompt = TranslationUnitsPromptBuilder.Build(content, false, true);
        var segments = JsonNode.Parse(prompt.Json)!.AsArray();

        Assert.IsTrue(prompt.HasTranslationMemorySuggestions);
        CollectionAssert.AreEqual(new[] { "{1&gt;INFORME DE MUESTRA&lt;1}|100%" }, GetSuggestions(segments, "{1&gt;SAMPLE REPORT&lt;1}"));
        CollectionAssert.AreEqual(new[] { "Solo el 7 % de los equipos de proyecto aplica los cuatro pasos clave.|64%" }, GetSuggestions(segments, "Only 7% of teams apply all four steps."));
        CollectionAssert.AreEqual(new[] { "Segunda frase.|85%" }, GetSuggestions(segments, "Second sentence."));
        Assert.IsNull(FindSegment(segments, "From idea to practice")[SuggestionsPropertyName]);
        Assert.IsNull(FindSegment(segments, "First sentence.")[SuggestionsPropertyName]);
    }

    [TestMethod]
    public async Task Build_with_tm_suggestions_ignores_machine_translation_alternatives()
    {
        var content = await LoadTransformation(PhraseFileName);

        var prompt = TranslationUnitsPromptBuilder.Build(content, false, true);
        var allSuggestionTargets = JsonNode.Parse(prompt.Json)!.AsArray()
            .SelectMany(segment => segment![SuggestionsPropertyName]?.AsArray() ?? [])
            .Select(suggestion => suggestion!["Target"]!.GetValue<string>())
            .ToList();

        CollectionAssert.DoesNotContain(allSuggestionTargets, "Desde la idea hasta la práctica");
        CollectionAssert.DoesNotContain(allSuggestionTargets, "Solamente 7% de equipos aplican cuatro pasos.");
    }

    [TestMethod]
    public async Task Build_without_tm_suggestions_keeps_only_id_source_and_target()
    {
        var content = await LoadTransformation(PhraseFileName);

        var prompt = TranslationUnitsPromptBuilder.Build(content, false, false);
        var segments = JsonNode.Parse(prompt.Json)!.AsArray();

        Assert.IsFalse(prompt.HasTranslationMemorySuggestions);
        Assert.AreEqual(5, segments.Count);
        foreach (var segment in segments)
        {
            CollectionAssert.AreEqual(new[] { "Id", "Source", "Target" }, segment!.AsObject().Select(property => property.Key).ToArray());
        }
    }

    [TestMethod]
    public async Task Build_with_tm_suggestions_for_file_without_alt_trans_reports_no_suggestions()
    {
        var content = await LoadTransformation(FileWithoutAltTransName);

        var prompt = TranslationUnitsPromptBuilder.Build(content, false, true);

        Assert.IsFalse(prompt.HasTranslationMemorySuggestions);
        Assert.IsFalse(prompt.Json.Contains(SuggestionsPropertyName));
    }

    private static async Task<Transformation> LoadTransformation(string fileName)
    {
        var stream = await new FileManager().DownloadAsync(new FileReference { Name = fileName });
        var loadResult = Transformation.Load(stream, fileName);
        Assert.IsTrue(loadResult.Success, loadResult.Error);
        return loadResult.Value;
    }

    private static JsonNode FindSegment(JsonArray segments, string source) =>
        segments.Single(segment => segment!["Source"]!.GetValue<string>() == source)!;

    private static string[] GetSuggestions(JsonArray segments, string source) =>
        FindSegment(segments, source)[SuggestionsPropertyName]!.AsArray()
            .Select(suggestion => $"{suggestion!["Target"]!.GetValue<string>()}|{suggestion["MatchQuality"]!.GetValue<string>()}")
            .ToArray();
}
