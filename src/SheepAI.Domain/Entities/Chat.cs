namespace SheepAI.Domain.Entities;

public sealed class Chat
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Name { get; set; }
    public bool IsUrgent { get; set; } = false;
    public bool IsAdminTaken { get; set; } = false;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; init; } = [];
}
