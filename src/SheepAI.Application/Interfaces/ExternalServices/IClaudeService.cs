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
    Task<DocumentUploadResult> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Sends a prompt about a previously uploaded document (identified by <paramref name="fileId"/>) and returns the completion.</summary>
    Task<ClaudeResult> AnalyzeDocumentAsync(string fileId, string prompt, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file from the Anthropic Files API.</summary>
    Task DeleteDocumentAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a multi-turn conversation grounded in the uploaded city documents.
    /// <paramref name="fileIds"/> are Anthropic Files API IDs already on the server.
    /// <paramref name="history"/> is the prior conversation ordered oldest-first; roles are
    /// <c>"user"</c>, <c>"assistant"</c>, or <c>"admin"</c> (admin is treated as assistant).
    /// </summary>
    Task<ClaudeResult> ChatWithDocumentsAsync(
        IReadOnlyList<string> fileIds,
        IReadOnlyList<(string Role, string Content)> history,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Checks whether a conversation is urgent and needs admin attention.
    /// Intended to be called in the background after the chat response has already been returned.
    /// </summary>
    Task<bool> CheckUrgencyAsync(
        IReadOnlyList<(string Role, string Content)> history,
        CancellationToken ct = default);
}
