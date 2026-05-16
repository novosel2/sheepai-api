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
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Uploading file {FileName} ({Size} bytes)", fileName, sizeBytes);

        var uploadResult = await claudeService.UploadDocumentAsync(fileStream, fileName, contentType, sizeBytes, ct);

        var cityFile = new CityFile
        {
            Name             = displayName,
            AnthropicFileId  = uploadResult.FileId
        };
        db.Files.Add(cityFile);
        await db.SaveChangesAsync(ct);

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

        logger.LogInformation("File deleted: {DisplayName} (Anthropic ID {AnthropicFileId})", cityFile.Name, cityFile.AnthropicFileId);
    }

    private static FileResponse MapToResponse(CityFile f) =>
        new(f.Id, f.Name, f.AnthropicFileId, f.CreatedAt);

    private ILogger<FileService> _logger => logger;
}
