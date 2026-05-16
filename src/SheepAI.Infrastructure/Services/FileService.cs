using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SheepAI.Application.DTOs.Responses.Admin;
using SheepAI.Application.Interfaces.ExternalServices;
using SheepAI.Application.Interfaces.Services;
using SheepAI.Domain.Entities;
using SheepAI.Domain.Exceptions;
using SheepAI.Infrastructure.Persistence;

namespace SheepAI.Infrastructure.Services;

public sealed class FileService(
    AppDbContext db,
    IClaudeService claudeService,
    ICacheService cacheService,
    ILogger<FileService> logger) : IFileService
{
    public async Task<List<FileResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var files = await db.Files
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return files.Select(MapToResponse).ToList();
    }

    public async Task<FileResponse> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string displayName,
        string category,
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Uploading file {FileName} ({Size} bytes)", fileName, sizeBytes);

        var uploadResult = await claudeService.UploadDocumentAsync(fileStream, fileName, contentType, sizeBytes, ct);

        var cityFile = new CityFile
        {
            Name            = displayName,
            Category        = category,
            AnthropicFileId = uploadResult.FileId
        };
        db.Files.Add(cityFile);
        await db.SaveChangesAsync(ct);
        await TryInvalidateFileIdsCacheAsync();

        logger.LogInformation("File uploaded: {DisplayName} → Anthropic ID {AnthropicFileId}", displayName, uploadResult.FileId);
        return MapToResponse(cityFile);
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        var cityFile = await db.Files.FindAsync([fileId], ct)
            ?? throw new NotFoundException($"File {fileId} not found.");

        await claudeService.DeleteDocumentAsync(cityFile.AnthropicFileId, ct);

        db.Files.Remove(cityFile);
        await db.SaveChangesAsync(ct);
        await TryInvalidateFileIdsCacheAsync();

        logger.LogInformation("File deleted: {DisplayName} (Anthropic ID {AnthropicFileId})", cityFile.Name, cityFile.AnthropicFileId);
    }

    private async Task TryInvalidateFileIdsCacheAsync()
    {
        try
        {
            await cacheService.RemoveAsync(ChatService.FileIdsCacheKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate file IDs cache; it will expire naturally");
        }
    }

    private static FileResponse MapToResponse(CityFile f) =>
        new(f.Id, f.Name, f.Category, f.AnthropicFileId, f.CreatedAt);

    private ILogger<FileService> _logger => logger;
}
