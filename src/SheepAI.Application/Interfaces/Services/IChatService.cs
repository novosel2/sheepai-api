using SheepAI.Application.DTOs.Responses.Chats;

namespace SheepAI.Application.Interfaces.Services;

/// <summary>Manages citizen chat sessions and message flow (AI or admin).</summary>
public interface IChatService
{
    /// <summary>Creates a new anonymous chat session and returns its ID.</summary>
    Task<CreateChatResponse> CreateChatAsync(CancellationToken ct = default);

    /// <summary>Returns all messages for the given chat in chronological order.</summary>
    Task<List<MessageResponse>> GetMessagesAsync(Guid chatId, CancellationToken ct = default);

    /// <summary>
    /// Saves the user message and — if the chat has not been taken over by an admin —
    /// calls Claude and returns the AI response.
    /// Returns <c>null</c> when admin has taken over (message is saved but no response is generated;
    /// the caller should return 204 so the frontend does not display a duplicate message).
    /// </summary>
    Task<MessageResponse?> SendMessageAsync(Guid chatId, string content, CancellationToken ct = default);

    /// <summary>Returns the admin-takeover status of the given chat.</summary>
    Task<ChatStatusResponse> GetStatusAsync(Guid chatId, CancellationToken ct = default);
}
