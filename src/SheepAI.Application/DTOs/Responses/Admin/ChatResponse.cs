namespace SheepAI.Application.DTOs.Responses.Admin;

public sealed record ChatResponse(
    Guid Id,
    string? Name,
    string? Summary,
    DateTime CreatedAt,
    bool IsUrgent,
    bool IsAdminTaken,
    DateTime LastMessageAt);
