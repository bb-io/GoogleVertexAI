using Apps.GoogleVertexAI.Models.Dto;

namespace Apps.GoogleVertexAI.Models.Entities;

public record class GetTranslationsEntity
{
    public Dictionary<string, string> Translations { get; init; } = new();
    public List<string> ErrorMessages { get; init; } = new();
    public UsageDto Usage { get; init; } = new();

    public GetTranslationsEntity() { }

    public GetTranslationsEntity(Dictionary<string, string> translations, List<string> errorMessages, UsageDto usage) 
    {
        Translations = translations;
        ErrorMessages = errorMessages;
        Usage = usage;
    }
}