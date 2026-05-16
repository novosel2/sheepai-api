using System.ComponentModel.DataAnnotations;

namespace SheepAI.Application.DTOs.Requests.Admin;

public sealed class AdminSendMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public required string Content { get; init; }
}
