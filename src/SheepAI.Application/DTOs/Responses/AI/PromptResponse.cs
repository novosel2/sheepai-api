namespace SheepAI.Application.DTOs.Responses.AI;

public sealed record PromptResponse(string Response, int InputTokens, int OutputTokens);
