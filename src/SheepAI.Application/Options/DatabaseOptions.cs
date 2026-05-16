namespace SheepAI.Application.Options;

public sealed class DatabaseOptions
{
    public static string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
}
