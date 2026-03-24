using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Clients;

public abstract class GenerativeModelClientBase
{
    protected static IEnumerable<SafetySetting> BuildVertexSafetySettings(PromptRequest promptRequest)
    {
        if (promptRequest is not { SafetyCategories: not null, SafetyCategoryThresholds: not null })
        {
            return Enumerable.Empty<SafetySetting>();
        }

        return promptRequest.SafetyCategories
            .Take(Math.Min(promptRequest.SafetyCategories.Count(), promptRequest.SafetyCategoryThresholds.Count()))
            .Zip(promptRequest.SafetyCategoryThresholds,
                (category, threshold) => new SafetySetting
                {
                    Category = Enum.Parse<HarmCategory>(category),
                    Threshold = Enum.Parse<SafetySetting.Types.HarmBlockThreshold>(threshold)
                });
    }

    protected static List<GeminiSafetySetting>? BuildSafetySettings(PromptRequest promptRequest)
    {
        if (promptRequest is not { SafetyCategories: not null, SafetyCategoryThresholds: not null })
        {
            return null;
        }

        return promptRequest.SafetyCategories
            .Take(Math.Min(promptRequest.SafetyCategories.Count(), promptRequest.SafetyCategoryThresholds.Count()))
            .Zip(promptRequest.SafetyCategoryThresholds,
                (category, threshold) => new GeminiSafetySetting
                {
                    Category = MapSafetyCategory(category),
                    Threshold = MapSafetyThreshold(threshold)
                })
            .ToList();
    }

    protected static UsageDto ExtractUsage(GeminiGenerateContentResponse response)
    {
        return response.UsageMetadata is null
            ? new UsageDto()
            : new UsageDto(
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount,
                response.UsageMetadata.TotalTokenCount);
    }

    protected static string ExtractText(GeminiGenerateContentResponse response)
    {
        var candidate = response.Candidates?.FirstOrDefault();
        return string.Concat(candidate?.Content?.Parts?
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text) ?? []);
    }

    protected static string MapSafetyCategory(string category)
    {
        return category switch
        {
            "SexuallyExplicit" => "HARM_CATEGORY_SEXUALLY_EXPLICIT",
            "HateSpeech" => "HARM_CATEGORY_HATE_SPEECH",
            "Harassment" => "HARM_CATEGORY_HARASSMENT",
            "DangerousContent" => "HARM_CATEGORY_DANGEROUS_CONTENT",
            _ => throw new PluginApplicationException($"Unsupported safety category: {category}")
        };
    }

    protected static string MapSafetyThreshold(string threshold)
    {
        return threshold switch
        {
            "BlockNone" => "BLOCK_NONE",
            "BlockLowAndAbove" => "BLOCK_LOW_AND_ABOVE",
            "BlockMediumAndAbove" => "BLOCK_MEDIUM_AND_ABOVE",
            "BlockOnlyHigh" => "BLOCK_ONLY_HIGH",
            _ => throw new PluginApplicationException($"Unsupported safety threshold: {threshold}")
        };
    }
}
