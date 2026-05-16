using SheepAI.Application.DTOs.Requests.Auth;
using SheepAI.Application.DTOs.Responses.Auth;

namespace SheepAI.Application.Interfaces.Services;

/// <summary>Handles admin authentication — registration, login, and logout.</summary>
public interface IAuthService
{
    /// <summary>Registers a new admin user and returns an access token.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Authenticates an admin user and returns an access token.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Blocklists the access token's jti in Redis so it cannot be reused.</summary>
    Task LogoutAsync(Guid userId, string accessToken, CancellationToken ct = default);
}
