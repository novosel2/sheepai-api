using SheepAI.Application.DTOs.Responses.Admin;
using SheepAI.Application.DTOs.Responses.Chats;

namespace SheepAI.Application.Interfaces.Services;

/// <summary>Admin-side chat operations: listing, takeover, and manual messaging.</summary>
public interface IChatAdminService
{
    /// <summary>Returns active (non-finished) chat sessions ordered by urgency then last activity.</summary>
    Task<List<ChatResponse>> GetAllChatsAsync(CancellationToken ct = default);

    /// <summary>Returns finished chat sessions ordered by most recent activity.</summary>
    Task<List<ChatResponse>> GetFinishedChatsAsync(CancellationToken ct = default);

    /// <summary>Marks the chat as admin-taken, disabling AI responses for that session.</summary>
    Task TakeOverAsync(Guid chatId, CancellationToken ct = default);

    /// <summary>Sends a manual admin message to the citizen in the given chat.</summary>
    Task<MessageResponse> SendMessageAsync(Guid chatId, string content, CancellationToken ct = default);
}
