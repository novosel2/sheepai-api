namespace SheepAI.Application.DTOs.Responses.Admin;

public sealed record FileResponse(
    Guid Id,
    string Name,
    string Category,
    string AnthropicFileId,
    DateTime CreatedAt);
