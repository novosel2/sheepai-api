using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.Auth;
using SheepAI.Application.DTOs.Responses.Auth;
using System.Security.Claims;
using SheepAI.Application.Interfaces.Services;

namespace SheepAI.API.Controllers.v1.Auth;

/// <summary>Handles admin authentication — registration, login, and logout.</summary>
[ApiController]
[Route("api/admin")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registers a new admin user and returns an access token.</summary>
    [HttpPost("register")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return Created(string.Empty, Api.Data("Registered successfully.", result));
    }

    /// <summary>Authenticates an admin user and returns an access token.</summary>
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return Ok(Api.Data("Logged in.", result));
    }

    /// <summary>Blocklists the current access token so it cannot be reused after logout.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);
        var accessToken = HttpContext.Request.Headers.Authorization.ToString()["Bearer ".Length..];
        await authService.LogoutAsync(userId, accessToken, ct);
        return NoContent();
    }
}
