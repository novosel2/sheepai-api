namespace SheepAI.Application.DTOs.Responses.Chats;

public sealed record ChatStatusResponse(string Name, string Summary, bool IsAdminTaken);
