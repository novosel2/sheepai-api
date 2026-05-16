namespace SheepAI.Application.Options;

public sealed class ClaudeOptions
{
    public static string SectionName = "Claude";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-opus-4-6";
    public int MaxTokens { get; set; } = 4096;
}
