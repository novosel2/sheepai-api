using SheepAI.Application.DTOs.Requests.Auth;
using SheepAI.Application.DTOs.Responses.Auth;

namespace SheepAI.Application.Interfaces.Services;

/// <summary>Handles user registration, login, token refresh, and logout.</summary>
public interface IAuthService
{
    /// <summary>Registers a new user and returns tokens.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Authenticates a user and returns tokens.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Issues new tokens in exchange for a valid refresh token.</summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Deletes all refresh tokens for the user and blocklists the access token's jti in Redis.</summary>
    Task LogoutAsync(Guid userId, string accessToken, CancellationToken ct = default);
}
