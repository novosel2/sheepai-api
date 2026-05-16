namespace SheepAI.Application.DTOs.Responses.Auth;

public sealed record AuthResponse(string AccessToken, int ExpiresIn);
