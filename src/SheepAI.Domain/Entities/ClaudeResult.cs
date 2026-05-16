namespace SheepAI.Domain.Entities;

public sealed record ClaudeResult(string Text, int InputTokens, int OutputTokens);
