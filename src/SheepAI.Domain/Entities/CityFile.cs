namespace SheepAI.Domain.Entities;

/// <summary>
/// A city PDF document uploaded by an admin. Stored via the Anthropic Files API.
/// </summary>
public sealed class CityFile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string Name { get; set; }
    public required string AnthropicFileId { get; init; }
}
