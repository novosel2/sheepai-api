using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.Admin;
using SheepAI.Application.DTOs.Responses.Admin;
using SheepAI.Application.DTOs.Responses.Chats;
using SheepAI.Application.Interfaces.Services;

namespace SheepAI.API.Controllers.v1.Admin;

/// <summary>Admin endpoints for viewing and managing citizen chats.</summary>
[ApiController]
[Authorize]
[Route("api/admin/chats")]
public sealed class AdminChatsController(IChatAdminService chatAdminService) : ControllerBase
{
    /// <summary>Returns active (non-finished) chat sessions, urgent ones first, then by most recent activity.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<List<ChatResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var chats = await chatAdminService.GetAllChatsAsync(ct);
        return Ok(Api.Data("Chats retrieved.", chats));
    }

    /// <summary>Returns finished chat sessions ordered by most recent activity.</summary>
    [HttpGet("finished")]
    [ProducesResponseType<ApiResponse<List<ChatResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFinished(CancellationToken ct)
    {
        var chats = await chatAdminService.GetFinishedChatsAsync(ct);
        return Ok(Api.Data("Finished chats retrieved.", chats));
    }

    /// <summary>
    /// Admin takes over a chat — sets is_admin_taken to true, which disables AI responses
    /// for that session. The citizen's next message will be saved but not answered by AI.
    /// </summary>
    [HttpPost("{chatId:guid}/take")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TakeOver(Guid chatId, CancellationToken ct)
    {
        await chatAdminService.TakeOverAsync(chatId, ct);
        return NoContent();
    }

    /// <summary>Admin sends a manual message to the citizen. Saved with role "admin".</summary>
    [HttpPost("{chatId:guid}/messages")]
    [ProducesResponseType<ApiResponse<MessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(Guid chatId, [FromBody] AdminSendMessageRequest request, CancellationToken ct)
    {
        var message = await chatAdminService.SendMessageAsync(chatId, request.Content, ct);
        return Ok(Api.Data("Message sent.", message));
    }
}
