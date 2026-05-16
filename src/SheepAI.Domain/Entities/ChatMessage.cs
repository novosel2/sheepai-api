namespace SheepAI.Domain.Entities;

/// <summary>
/// A single message in a chat. Role is one of: "user", "assistant", "admin".
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ChatId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string Role { get; init; }
    public required string Content { get; init; }

    public Chat Chat { get; init; } = null!;
}
