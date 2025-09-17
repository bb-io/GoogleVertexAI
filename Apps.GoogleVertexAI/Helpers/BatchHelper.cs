using Apps.GoogleVertexAI.Utils;
using Apps.GoogleVertexAI.Models.Requests;

namespace Apps.GoogleVertexAI.Helpers;
public class BatchHelper()
{
    public static object BuildBatchRequestObject(string userPrompt, string systemPrompt, PromptRequest pr, string modelPath)
    {
        var genCfg = new Dictionary<string, object>();
        if (pr.Temperature.HasValue) genCfg["temperature"] = pr.Temperature.Value;
        if (pr.TopP.HasValue) genCfg["topP"] = pr.TopP.Value;
        if (pr.TopK.HasValue) genCfg["topK"] = pr.TopK.Value;
        if (pr.MaxOutputTokens.HasValue) 
            genCfg["maxOutputTokens"] = pr.MaxOutputTokens.Value;
        else
            genCfg["maxOutputTokens"] = ModelTokenService.GetMaxTokensForModel(modelPath);

        var system = new { parts = new[] { new { text = systemPrompt } } };
        var contents = new[]
        {
        new
        {
            role = "user",
            parts = new object[] { new { text = userPrompt } }
        }
            };

        if (genCfg.Count == 0)
        {
            return new
            {
                request = new
                {
                    systemInstruction = system,
                    contents = contents
                }
            };
        }
        else
        {
            return new
            {
                request = new
                {
                    systemInstruction = system,
                    contents = contents,
                    generationConfig = genCfg
                }
            };
        }
    }

    
}
