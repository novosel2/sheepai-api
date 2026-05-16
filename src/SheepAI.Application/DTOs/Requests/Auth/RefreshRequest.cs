using System.ComponentModel.DataAnnotations;

namespace SheepAI.Application.DTOs.Requests.Auth;

public sealed record RefreshRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;
}
