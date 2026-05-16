namespace SheepAI.Application.Options;

/// <summary>Cache TTL tiers in minutes. All values are configurable via <c>Cache:Ttl</c> in appsettings.</summary>
public sealed class CacheTtlOptions
{
    public static string SectionName = "Cache:Ttl";

    /// <summary>Short-lived data (lists, single fetches). Default: 5 min.</summary>
    public int Short { get; init; } = 5;

    /// <summary>Identity lookups, membership checks. Default: 15 min.</summary>
    public int Medium { get; init; } = 15;

    /// <summary>Permission sets. Default: 20 min.</summary>
    public int Long { get; init; } = 20;

    /// <summary>Static reference data that rarely changes. Default: 60 min.</summary>
    public int VeryLong { get; init; } = 60;
}
