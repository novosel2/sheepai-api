namespace SheepAI.Application.DTOs.Responses.Documents;

/// <summary>Details of a document uploaded to the Anthropic Files API.</summary>
/// <param name="FileId">Stable identifier used in subsequent analyze calls.</param>
/// <param name="FileName">Original file name as provided at upload time.</param>
/// <param name="SizeBytes">File size in bytes.</param>
public sealed record UploadDocumentResponse(string FileId, string FileName, long SizeBytes);
