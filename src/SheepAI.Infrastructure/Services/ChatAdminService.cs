using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SheepAI.Application.DTOs.Responses.Admin;
using SheepAI.Application.DTOs.Responses.Chats;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Domain.Entities;
using SheepAI.Domain.Exceptions;
using SheepAI.Infrastructure.Persistence;

namespace SheepAI.Infrastructure.Services;

public sealed class ChatAdminService(
    AppDbContext db,
    ILogger<ChatAdminService> logger) : IChatAdminService
{
    public async Task<List<ChatResponse>> GetAllChatsAsync(CancellationToken ct = default)
    {
        var chats = await db.Chats
            .AsNoTracking()
            .OrderByDescending(c => c.IsUrgent)
            .ThenByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);

        return chats.Select(MapToResponse).ToList();
    }

    public async Task TakeOverAsync(Guid chatId, CancellationToken ct = default)
    {
        var chat = await db.Chats.FindAsync([chatId], ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        if (chat.IsAdminTaken) return;

        chat.IsAdminTaken = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin took over chat {ChatId}", chatId);
    }

    public async Task<MessageResponse> SendMessageAsync(Guid chatId, string content, CancellationToken ct = default)
    {
        var chat = await db.Chats.FindAsync([chatId], ct)
            ?? throw new NotFoundException($"Chat {chatId} not found.");

        var message = new ChatMessage { ChatId = chatId, Role = "admin", Content = content };
        db.ChatMessages.Add(message);

        chat.LastMessageAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin sent message to chat {ChatId}", chatId);
        return new MessageResponse(message.Id, message.ChatId, message.Role, message.Content, message.CreatedAt);
    }

    private static ChatResponse MapToResponse(Chat c) =>
        new(c.Id, c.CreatedAt, c.IsUrgent, c.IsAdminTaken, c.LastMessageAt);
}
