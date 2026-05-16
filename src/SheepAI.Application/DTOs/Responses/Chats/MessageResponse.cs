namespace SheepAI.Application.DTOs.Responses.Chats;

public sealed record MessageResponse(
    Guid Id,
    Guid ChatId,
    string Role,
    string Content,
    DateTime CreatedAt);
