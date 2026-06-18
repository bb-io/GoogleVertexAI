namespace Apps.GoogleVertexAI.Constants;

public static class ResponseSchemas
{
    public static readonly object IdTranslationArray = new 
    {
        type = "ARRAY",
        items = new 
        { 
            type = "OBJECT",
            properties = new 
            {
                id = new { type = "INTEGER" },
                translation = new { type = "STRING" }
            },
            required = new[] { "id", "translation" }
        }
    };

    public static readonly object IdTargetsArray = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                id = new { type = "INTEGER" },
                targets = new
                {
                    type = "ARRAY",
                    items = new { type = "STRING" }
                }
            },
            required = new[] { "id", "targets" }
        }
    };
}
