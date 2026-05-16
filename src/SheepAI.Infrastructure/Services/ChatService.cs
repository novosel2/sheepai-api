using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SheepAI.Application.DTOs.Responses.Chats;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Domain.Entities;
using SheepAI.Domain.Exceptions;
using SheepAI.Infrastructure.Persistence;

namespace SheepAI.Infrastructure.Services;

public sealed class ChatService(
    AppDbContext db,
    IClaudeService claudeService,
    ILogger<ChatService> logger) : IChatService
{
    // Urgency triggers: more than this many user messages, or content matches keywords.
    private const int UrgencyMessageThreshold = 10;

    private static readonly string[] UrgencyKeywords =
    [
        "hitno", "hitna", "hitni", "pomoć", "pomoc", "sos", "opasnost",
        "nesreća", "nesreca", "upomoć", "upomoc", "žurno", "zurno", "urgent"
    ];

    public async Task<CreateChatResponse> CreateChatAsync(CancellationToken ct = default)
    {
        var chat = new Chat();
        db.Chats.Add(chat);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created chat {ChatId}", chat.Id);
        return new CreateChatResponse(chat.Id);
    }

    public async Task<List<MessageResponse>> GetMessagesAsync(Guid chatId, CancellationToken ct = default)
    {
        var exists = await db.Chats.AnyAsync(c => c.Id == chatId, ct);
        if (!exists)
            throw new NotFoundException($"Chat {chatId} not found.");

        var messages = await db.ChatMessages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return messages.Select(MapToResponse).ToList();
    }

    public async Task<MessageResponse> SendMessageAsync(Guid chatId, string content, CancellationToken ct = default)
    {
        var chat = await db.Chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatId, ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        var now = DateTime.UtcNow;
        chat.LastMessageAt = now;

        // Save user message
        var userMsg = new ChatMessage { ChatId = chatId, Role = "user", Content = content };
        db.ChatMessages.Add(userMsg);

        // If admin has taken over, no AI response — just persist and return the user message.
        if (chat.IsAdminTaken)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Chat {ChatId} is admin-taken; user message saved without AI response", chatId);
            return MapToResponse(userMsg);
        }

        // Build history from existing messages (before the current one)
        var history = chat.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => (m.Role, m.Content))
            .ToList();

        // Fetch all uploaded document file IDs for grounding
        var fileIds = await db.Files
            .Select(f => f.AnthropicFileId)
            .ToListAsync(ct);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Calling Claude for chat {ChatId} with {FileCount} docs and {HistoryCount} history turns",
                chatId, fileIds.Count, history.Count);

        // Call Claude
        var claudeResult = await claudeService.ChatWithDocumentsAsync(fileIds, history, content, ct);

        var assistantMsg = new ChatMessage { ChatId = chatId, Role = "assistant", Content = claudeResult.Text };
        db.ChatMessages.Add(assistantMsg);

        // Urgency check
        if (!chat.IsUrgent)
        {
            var userMessageCount = chat.Messages.Count(m => m.Role == "user") + 1; // +1 for current
            if (userMessageCount >= UrgencyMessageThreshold || IsUrgentContent(content))
            {
                chat.IsUrgent = true;
                logger.LogInformation("Chat {ChatId} flagged as urgent", chatId);
            }
        }

        await db.SaveChangesAsync(ct);
        return MapToResponse(assistantMsg);
    }

    public async Task<ChatStatusResponse> GetStatusAsync(Guid chatId, CancellationToken ct = default)
    {
        var chat = await db.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId, ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        return new ChatStatusResponse(chat.IsAdminTaken);
    }

    private static MessageResponse MapToResponse(ChatMessage m) =>
        new(m.Id, m.ChatId, m.Role, m.Content, m.CreatedAt);

    private static bool IsUrgentContent(string content)
    {
        var lower = content.ToLowerInvariant();
        return UrgencyKeywords.Any(kw => lower.Contains(kw));
    }

    private ILogger<ChatService> _logger => logger;
}
