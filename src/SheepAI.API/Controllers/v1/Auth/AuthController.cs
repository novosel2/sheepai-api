using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.Auth;
using SheepAI.Application.DTOs.Responses.Auth;
using System.Security.Claims;
using SheepAI.Application.Interfaces.Services;

namespace SheepAI.API.Controllers.v1.Auth;

/// <summary>Handles user registration, login, token refresh, and logout.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registers a new user and returns access + refresh tokens.</summary>
    [HttpPost("register")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return Created(string.Empty, Api.Data("Registered successfully.", result));
    }

    /// <summary>Authenticates a user and returns access + refresh tokens.</summary>
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return Ok(Api.Data("Logged in.", result));
    }

    /// <summary>Issues new tokens in exchange for a valid refresh token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request.Token, ct);
        return Ok(Api.Data("Token refreshed.", result));
    }

    /// <summary>Invalidates all sessions for the current user and blocklists the access token.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);
        var accessToken = HttpContext.Request.Headers.Authorization.ToString()["Bearer ".Length..];
        await authService.LogoutAsync(userId, accessToken, ct);
        return NoContent();
    }
}
