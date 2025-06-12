using Blackbird.Xliff.Utils.Models;

namespace Apps.GoogleVertexAI.Utils;


public static class TranslationUnitExtensions
{
    public static bool IsLocked(this TranslationUnit translationUnit)
    {
        if (translationUnit.Attributes.TryGetValue("locked", out var locked))
        {
            if (locked == "locked" || locked == "true")
            {
                return true;
            }
        }

        return false;
    }
}