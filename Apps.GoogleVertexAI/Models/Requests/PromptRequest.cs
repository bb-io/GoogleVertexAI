using Apps.GoogleVertexAI.DataSourceHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.FloatParameterHandlers;
using Apps.GoogleVertexAI.DataSourceHandlers.Static;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GoogleVertexAI.Models.Requests;

public class PromptRequest
{
    [Display("Temperature", Description = "Temperature controls the degree of randomness in token selection. " +
                                          "Lower temperatures are good for prompts that require a more " +
                                          "deterministic and less open-ended or creative response, while " +
                                          "higher temperatures can lead to more diverse or creative results.")]
    [DataSource(typeof(TemperatureDataSourceHandler))]
    public float? Temperature { get; set; }

    [Display("Max output tokens", Description = "Maximum number of tokens that can be generated in the response. " +
                                                "Each token is about four characters, and 100 tokens translate to " +
                                                "approximately 60-80 words. Maximum value for prompts without " +
                                                "media is 8192, otherwise, 2048.")]
    public int? MaxOutputTokens { get; set; }

    [Display("Top-K", Description = "Top-K changes how the model selects tokens for output. For example, a " +
                                    "top-K of 1 selects the most probable token, while a top-K of 3 chooses " +
                                    "from the three most probable tokens using temperature. Specify a lower " +
                                    "value for less random responses and a higher value for more random responses.")]
    [DataSource(typeof(TopKDataSourceHandler))]
    public int? TopK { get; set; }

    [Display("Top-P", Description = "Top-P selects tokens from most to least probable until their cumulative " +
                                    "probability equals the specified top-P value. For instance, if tokens A, " +
                                    "B, and C have probabilities 0.3, 0.2, and 0.1, and the top-P value is 0.5, " +
                                    "either A or B will be selected using temperature, excluding C. Specify a lower " +
                                    "value for less random responses and a higher value for more random responses.")]
    [DataSource(typeof(TopPDataSourceHandler))]
    public float? TopP { get; set; }

    [Display("Safety categories", Description = "The safety categories to configure thresholds for. For each " +
                                                "specified category, a respective threshold should be added in " +
                                                "the 'Thresholds for safety categories' input parameter.")]
    [DataSource(typeof(SafetyCategoryDataSourceHandler))]
    public IEnumerable<string>? SafetyCategories { get; set; }

    [Display("Thresholds for safety categories", Description = "The thresholds to configure for safety " +
                                                               "categories. For each specified threshold, " +
                                                               "a respective category should be added in " +
                                                               "the 'Safety categories' input parameter.")]
    [DataSource(typeof(SafetyCategoryThresholdDataSourceHandler))]
    public IEnumerable<string>? SafetyCategoryThresholds { get; set; }
    
    [Display("Model endpoint")]
    [StaticDataSource(typeof(GeminiModelDataSourceHandler))]
    public string? ModelEndpoint { get; set; }
}