namespace SheepAI.Application.DTOs.Requests.Documents;

/// <summary>Request body for analyzing a previously uploaded document.</summary>
/// <param name="Prompt">The question or instruction to ask about the document.</param>
public sealed record AnalyzeDocumentRequest(string Prompt);
