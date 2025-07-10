using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Blueprints.Interfaces.Edit;

namespace Apps.GoogleVertexAI.Models.Response;
public class TextEditResponse : IEditTextOutput
{
    [Display("Edited text")]
    public string EditedText { get; set; }
}
