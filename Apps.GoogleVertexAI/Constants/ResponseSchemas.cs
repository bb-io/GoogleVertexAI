namespace Apps.GoogleVertexAI.Constants;

public static class ResponseSchemas
{
    public static readonly object ImageLocalizationExtraction = new
    {
        type = "OBJECT",
        properties = new
        {
            source_language = new
            {
                type = "STRING",
                description = "The BCP-47 language code detected in the image, or the source language supplied by the user."
            },
            segments = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        text = new
                        {
                            type = "STRING",
                            description = "One complete localizable text segment exactly as it appears in the image."
                        }
                    },
                    required = new[] { "text" }
                }
            }
        },
        required = new[] { "source_language", "segments" }
    };
}
