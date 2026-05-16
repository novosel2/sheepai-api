using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.DTOs.Requests.Chats;
using SheepAI.Application.DTOs.Responses.Chats;
using SheepAI.Application.Interfaces.Services;

namespace SheepAI.API.Controllers.v1.Chats;

/// <summary>Public (no auth) endpoints for citizen chat sessions.</summary>
[ApiController]
[Route("api/chats")]
public sealed class ChatsController(IChatService chatService) : ControllerBase
{
    /// <summary>Creates a new anonymous chat session. Returns the chat ID to store in the browser.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<CreateChatResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateChat(CancellationToken ct)
    {
        var result = await chatService.CreateChatAsync(ct);
        return Ok(Api.Data("Chat created.", result));
    }

    /// <summary>Returns all messages for the given chat in chronological order.</summary>
    [HttpGet("{chatId:guid}/messages")]
    [ProducesResponseType<ApiResponse<List<MessageResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(Guid chatId, CancellationToken ct)
    {
        var messages = await chatService.GetMessagesAsync(chatId, ct);
        return Ok(Api.Data("Messages retrieved.", messages));
    }

    /// <summary>
    /// Sends a user message and returns the AI response(s).
    /// Returns a list with one item for a plain text response, or two items when Claude includes a widget
    /// (first is <c>role=assistant</c> with the text, second is <c>role=widget</c> with widget JSON as content).
    /// If an admin has taken over this chat, returns 204 (no AI response is generated).
    /// </summary>
    [HttpPost("{chatId:guid}/messages")]
    [ProducesResponseType<ApiResponse<List<MessageResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(Guid chatId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var responses = await chatService.SendMessageAsync(chatId, request.Content, ct);
        return responses is null ? NoContent() : Ok(Api.Data("Message sent.", responses));
    }

    /// <summary>Marks the chat as finished. Idempotent — safe to call multiple times.</summary>
    [HttpPost("{chatId:guid}/finish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FinishChat(Guid chatId, CancellationToken ct)
    {
        await chatService.FinishChatAsync(chatId, ct);
        return NoContent();
    }

    /// <summary>
    /// Returns whether an admin has taken over this chat.
    /// Intended for client-side polling so the citizen knows to stop expecting AI replies.
    /// </summary>
    [HttpGet("{chatId:guid}/status")]
    [ProducesResponseType<ApiResponse<ChatStatusResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid chatId, CancellationToken ct)
    {
        var status = await chatService.GetStatusAsync(chatId, ct);
        return Ok(Api.Data("Status retrieved.", status));
    }
}
