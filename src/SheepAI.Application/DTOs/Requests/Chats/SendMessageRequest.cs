using System.ComponentModel.DataAnnotations;

namespace SheepAI.Application.DTOs.Requests.Chats;

public sealed class SendMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public required string Content { get; init; }
}
