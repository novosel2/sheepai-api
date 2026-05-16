using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SheepAI.Application.DTOs.Requests.Auth;
using SheepAI.Application.DTOs.Responses.Auth;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Application.Options;
using SheepAI.Domain.Entities;
using SheepAI.Domain.Exceptions;
using SheepAI.Infrastructure.Persistence;

namespace SheepAI.Infrastructure.Services;

public sealed class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    ICacheService cache,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <inheritdoc/>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email, ct))
            throw new ConflictException($"Email '{request.Email}' is already registered.");

        var user = new User
        {
            Email        = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} registered.", user.Id);

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc/>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        logger.LogInformation("User {UserId} logged in.", user.Id);

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc/>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow)
        {
            if (stored is not null)
                db.RefreshTokens.Remove(stored);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Refresh token is invalid or expired.");
        }

        db.RefreshTokens.Remove(stored);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(stored.User, ct);
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(Guid userId, string accessToken, CancellationToken ct = default)
    {
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ExecuteDeleteAsync(ct);

        // Blocklist the access token's jti for its remaining lifetime
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var remaining = jwt.ValidTo - DateTime.UtcNow;

            if (!string.IsNullOrEmpty(jwt.Id) && remaining > TimeSpan.Zero)
                await cache.SetAsync($"blocklist:{jwt.Id}", true, remaining, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to blocklist access token jti on logout.");
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = user.Id,
            Token     = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays)
        });

        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, _jwt.ExpiresInMinutes * 60);
    }

    private string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.ExpiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
