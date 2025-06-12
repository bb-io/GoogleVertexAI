using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Models.Entities;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Newtonsoft.Json;

namespace Apps.GoogleVertexAI.Utils;

public static class GeminiResponseParser
{
    public static ParseResultsEntity ParseStringArray(string response, Logger? logger = null)
    {
        try
        {
            string cleanedText = response.Trim()
                .Replace("```", string.Empty)
                .Replace("json", string.Empty);

            int startIndex = cleanedText.IndexOf("[");
            if (startIndex < 0)
            {
                throw new PluginApplicationException("Invalid response format: JSON array not found in response.");
            }

            string jsonContent = cleanedText.Substring(startIndex);
            var result = JsonConvert.DeserializeObject<string[]>(jsonContent);
            if (result != null)
            {
                return new ParseResultsEntity(result, false);
            }

            throw new PluginApplicationException("Failed to deserialize response as string array.");
        }
        catch (JsonReaderException)
        {
            var results = ExtractItemsFromIncompleteJsonArray(response);
            return new ParseResultsEntity(results, true);
        }
        catch (Exception ex) when (!(ex is PluginApplicationException))
        {
            logger?.LogError($"[GoogleGemini] Error parsing response: {ex.Message}. Attempting fallback parser.", []);
            throw;
        }
    }

    private static string[] ExtractItemsFromIncompleteJsonArray(string incompleteJson)
    {
        var result = new List<string>();
        var pattern = @"""(\{ID:[^""\\]*(?:\\.[^""\\]*)*})""";

        var matches = Regex.Matches(incompleteJson, pattern);
        if (matches.Count == 0)
        {
            pattern = @"\[\s*""(.+?)""\s*,?";
            matches = Regex.Matches(incompleteJson, pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1 && match.Groups[1].Success)
                {
                    result.Add(match.Groups[1].Value);
                }
            }
        }
        else
        {
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1 && match.Groups[1].Success)
                {
                    var unescaped = Regex.Replace(match.Groups[1].Value, @"\\(.)", m =>
                    {
                        char c = m.Groups[1].Value[0];
                        return c switch
                        {
                            'n' => "\n",
                            'r' => "\r",
                            't' => "\t",
                            'b' => "\b",
                            'f' => "\f",
                            '"' => "\"",
                            '\\' => "\\",
                            _ => m.Value
                        };
                    });

                    result.Add(unescaped);
                }
            }
        }

        return result.ToArray();
    }
}
