using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Translate;

namespace Apps.GoogleVertexAI.Models.Response;
public class TextTranslationResponse : ITranslateTextOutput
{
    [Display("Translated text")]
    public string TranslatedText { get; set; }
}
