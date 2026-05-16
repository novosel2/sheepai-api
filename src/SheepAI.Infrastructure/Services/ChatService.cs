using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheepAI.Application.DTOs.Responses.Chats;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Application.Options;
using SheepAI.Domain.Entities;
using SheepAI.Domain.Exceptions;
using SheepAI.Infrastructure.Persistence;

namespace SheepAI.Infrastructure.Services;

public sealed class ChatService(
    AppDbContext db,
    IClaudeService claudeService,
    ICacheService cacheService,
    IServiceScopeFactory scopeFactory,
    IOptions<CacheTtlOptions> cacheTtl,
    ILogger<ChatService> logger) : IChatService
{
    internal const string FileIdsCacheKey = "files:anthropic-ids";
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
        var messages = await db.ChatMessages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        // Common path (non-empty chat) costs one query.
        // Only fall back to an existence check when the list is empty to distinguish
        // "chat has no messages yet" from "chat does not exist".
        if (messages.Count == 0 && !await db.Chats.AnyAsync(c => c.Id == chatId, ct))
            throw new NotFoundException($"Chat {chatId} not found.");

        return messages.Select(MapToResponse).ToList();
    }

    public async Task<MessageResponse?> SendMessageAsync(Guid chatId, string content, CancellationToken ct = default)
    {
        var chat = await db.Chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatId, ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        chat.LastMessageAt = DateTime.UtcNow;

        var userMsg = new ChatMessage { ChatId = chatId, Role = "user", Content = content };
        db.ChatMessages.Add(userMsg);

        // Admin has taken over — save the message (admin can read it) but return null
        // so the controller sends 204 and the frontend doesn't show a duplicate.
        if (chat.IsAdminTaken)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Chat {ChatId} is admin-taken; user message saved, no response sent", chatId);
            return null;
        }

        var history = chat.Messages
            .OrderBy(m => m.CreatedAt)
            .TakeLast(50)
            .Select(m => (m.Role, m.Content))
            .ToList();

        var fileIds = await cacheService.GetOrSetAsync(
            FileIdsCacheKey,
            () => db.Files.Select(f => f.AnthropicFileId).ToListAsync(ct),
            TimeSpan.FromMinutes(cacheTtl.Value.VeryLong),
            ct);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Calling Claude for chat {ChatId} with {FileCount} docs and {HistoryCount} history turns",
                chatId, fileIds.Count, history.Count);

        var claudeResult = await claudeService.ChatWithDocumentsAsync(fileIds, history, content, ct);

        var assistantMsg = new ChatMessage { ChatId = chatId, Role = "assistant", Content = claudeResult.Text };
        db.ChatMessages.Add(assistantMsg);
        await db.SaveChangesAsync(ct);

        var fullHistory = history
            .Append((Role: "user", Content: content))
            .Append((Role: "assistant", Content: claudeResult.Text))
            .ToList();

        if (chat.Name is null)
            _ = GenerateAndSaveChatNameAsync(chatId, content);

        if (fullHistory.Count >= 2)
            _ = GenerateAndSaveChatSummaryAsync(chatId, fullHistory);

        if (!chat.IsUrgent)
            _ = CheckAndUpdateUrgencyAsync(chatId, fullHistory);

        return MapToResponse(assistantMsg);
    }

    public async Task<ChatStatusResponse> GetStatusAsync(Guid chatId, CancellationToken ct = default)
    {
        var chat = await db.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId, ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        return new ChatStatusResponse(
            chat.Name ?? $"Chat {chat.Id.ToString()[..8]}",
            chat.Summary ?? string.Empty,
            chat.IsAdminTaken);
    }

    public async Task FinishChatAsync(Guid chatId, CancellationToken ct = default)
    {
        var chat = await db.Chats.FindAsync([chatId], ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        if (chat.IsFinished) return;

        chat.IsFinished = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Chat {ChatId} marked as finished", chatId);
    }

    private async Task GenerateAndSaveChatSummaryAsync(Guid chatId, List<(string Role, string Content)> history)
    {
        try
        {
            var summary = await claudeService.GenerateChatSummaryAsync(history);
            if (string.IsNullOrWhiteSpace(summary)) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var chat = await scopedDb.Chats.FindAsync(chatId);
            if (chat is not null)
            {
                chat.Summary = summary;
                await scopedDb.SaveChangesAsync();
                logger.LogInformation("Chat {ChatId} summary updated", chatId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background summary generation failed for chat {ChatId}", chatId);
        }
    }

    private async Task GenerateAndSaveChatNameAsync(Guid chatId, string firstMessage)
    {
        try
        {
            var name = await claudeService.GenerateChatNameAsync(firstMessage);
            if (string.IsNullOrWhiteSpace(name)) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var chat = await scopedDb.Chats.FindAsync(chatId);
            if (chat is { Name: null })
            {
                chat.Name = name;
                await scopedDb.SaveChangesAsync();
                logger.LogInformation("Chat {ChatId} named: {Name}", chatId, name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background name generation failed for chat {ChatId}", chatId);
        }
    }

    private async Task CheckAndUpdateUrgencyAsync(Guid chatId, List<(string Role, string Content)> history)
    {
        try
        {
            var isUrgent = await claudeService.CheckUrgencyAsync(history);
            if (!isUrgent) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var chat = await scopedDb.Chats.FindAsync(chatId);
            if (chat is { IsUrgent: false })
            {
                chat.IsUrgent = true;
                await scopedDb.SaveChangesAsync();
                logger.LogInformation("Chat {ChatId} flagged as urgent", chatId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background urgency check failed for chat {ChatId}", chatId);
        }
    }

    private static MessageResponse MapToResponse(ChatMessage m) =>
        new(m.Id, m.ChatId, m.Role, m.Content, m.CreatedAt);

    private ILogger<ChatService> _logger => logger;
}
