using SheepAI.Application.DTOs.Responses.Admin;

namespace SheepAI.Application.Interfaces.Services;

/// <summary>Manages city PDF documents — uploads to Anthropic Files API and tracks them in the DB.</summary>
public interface IFileService
{
    /// <summary>Returns all uploaded city documents.</summary>
    Task<List<FileResponse>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Uploads a PDF to the Anthropic Files API and persists the record in the DB.</summary>
    Task<FileResponse> UploadAsync(Stream fileStream, string fileName, string displayName, long sizeBytes, CancellationToken ct = default);

    /// <summary>Deletes the document from both the Anthropic Files API and the DB.</summary>
    Task DeleteAsync(Guid fileId, CancellationToken ct = default);
}
