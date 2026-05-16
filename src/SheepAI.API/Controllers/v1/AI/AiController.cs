using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.AI;
using SheepAI.Application.DTOs.Responses.AI;
using SheepAI.Application.Interfaces.ExternalServices;

namespace SheepAI.API.Controllers.v1.AI;

/// <summary>Handles AI prompt requests via Claude.</summary>
[ApiController]
[Route("api/v1/ai")]
// [Authorize]
public sealed class AiController(IClaudeService claudeService, ILogger<AiController> logger) : ControllerBase
{
    /// <summary>Sends a prompt to Claude and returns the completion.</summary>
    [HttpPost("prompt")]
    [ProducesResponseType<ApiResponse<PromptResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Prompt([FromBody] PromptRequest request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Prompt request from user {UserId}", User.FindFirst("sub")?.Value);

        var result = await claudeService.GetCompletionAsync(request.Prompt, cancellationToken);
        var dto = new PromptResponse(result.Text, result.InputTokens, result.OutputTokens);
        return Ok(Api.Data("Prompt processed.", dto));
    }

    private ILogger<AiController> _logger => logger;
}
