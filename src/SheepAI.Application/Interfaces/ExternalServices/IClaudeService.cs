using SheepAI.Domain.Entities;

namespace SheepAI.Application.Interfaces.ExternalServices;

/// <summary>Anthropic Claude API client abstraction.</summary>
public interface IClaudeService
{
    /// <summary>Sends a text prompt and returns the completion.</summary>
    Task<ClaudeResult> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a PDF file to the Anthropic Files API and returns the stable file ID.
    /// The file ID can be reused across multiple <see cref="AnalyzeDocumentAsync"/> calls.
    /// </summary>
    Task<DocumentUploadResult> UploadDocumentAsync(Stream fileStream, string fileName, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Sends a prompt about a previously uploaded document (identified by <paramref name="fileId"/>) and returns the completion.</summary>
    Task<ClaudeResult> AnalyzeDocumentAsync(string fileId, string prompt, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file from the Anthropic Files API.</summary>
    Task DeleteDocumentAsync(string fileId, CancellationToken cancellationToken = default);
}
