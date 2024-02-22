namespace Apps.GoogleVertexAI.Models.Dtos;

public record ErrorDto(int Code, string Message, string Status);

public record ErrorDtoWrapper(ErrorDto Error);