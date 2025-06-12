using System.Text.RegularExpressions;
using Apps.GoogleVertexAI.Models.Dto;
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
    
    public static List<XliffIssueDto> ParseIssuesJson(string response, Logger? logger = null)
    {
        try
        {
            // First handle code blocks with triple backticks
            string jsonContent = response.Trim();
            if (jsonContent.Contains("```"))
            {
                var match = Regex.Match(jsonContent, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```");
                if (match.Success && match.Groups.Count > 1)
                {
                    jsonContent = match.Groups[1].Value.Trim();
                }
                else
                {
                    // Remove code block markers
                    jsonContent = jsonContent
                        .Replace("```json", "")
                        .Replace("```", "")
                        .Trim();
                }
            }
            
            // Try to find the JSON array start if it's not at the beginning
            if (!jsonContent.StartsWith("["))
            {
                int startIndex = jsonContent.IndexOf("[");
                if (startIndex >= 0)
                {
                    jsonContent = jsonContent.Substring(startIndex);
                }
                else
                {
                    return new List<XliffIssueDto>();
                }
            }
            
            int endIndex = jsonContent.LastIndexOf("]");
            if (endIndex > 0)
            {
                jsonContent = jsonContent.Substring(0, endIndex + 1);
            }
                        
            var issues = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(jsonContent);
            if (issues == null || !issues.Any())
            {
                return new List<XliffIssueDto>();
            }
            
            var result = new List<XliffIssueDto>();
            foreach (var issue in issues)
            {
                string? id = null;
                if (issue.TryGetValue("id", out var directId))
                {
                    id = directId;
                }
                else if (issue.TryGetValue("translation_unit_id", out var legacyId))
                {
                    id = legacyId;
                }
                
                if (id != null && issue.TryGetValue("issues", out var issueText))
                {
                    result.Add(new XliffIssueDto
                    {
                        Id = id,
                        Issues = issueText
                    });
                }
            }
            
            return result;
        }
        catch (JsonReaderException ex)
        {
            logger?.LogError($"[GoogleGemini] JSON parsing error: {ex.Message}", []);
            throw new PluginApplicationException($"Failed to parse JSON response: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger?.LogError($"[GoogleGemini] Error processing issues: {ex.Message}", []);
            throw new PluginApplicationException($"Unexpected error processing translation issues: {ex.Message}");
        }
    }
}
