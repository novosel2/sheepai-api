namespace SheepAI.Domain.Entities;

public sealed record DocumentUploadResult(string FileId, string FileName, long SizeBytes);
