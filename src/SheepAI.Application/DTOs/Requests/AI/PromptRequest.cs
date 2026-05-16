using System.ComponentModel.DataAnnotations;

namespace SheepAI.Application.DTOs.Requests.AI;

public sealed record PromptRequest
{
    [Required]
    [MaxLength(10_000)]
    public string Prompt { get; init; } = string.Empty;
}
