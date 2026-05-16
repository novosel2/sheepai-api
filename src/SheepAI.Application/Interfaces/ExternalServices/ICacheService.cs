namespace SheepAI.Application.Interfaces.ExternalServices;

/// <summary>
/// Distributed cache abstraction. Use <see cref="GetOrSetAsync{T}"/> for cache-aside patterns.
/// Do not cache ValueTuples (System.Text.Json skips struct fields) or interfaces (cannot be deserialized).
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value for <paramref name="key"/>, or <c>null</c> if not found.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> with the given <paramref name="ttl"/>.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Removes the entry for <paramref name="key"/> if it exists.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all cache entries whose keys match the Redis glob <paramref name="pattern"/>.</summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>; if not found, invokes <paramref name="factory"/>,
    /// stores the result with <paramref name="ttl"/>, and returns it.
    /// Concurrent requests for the same uncached key share a single in-flight fetch (thundering herd prevention).
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);
}
