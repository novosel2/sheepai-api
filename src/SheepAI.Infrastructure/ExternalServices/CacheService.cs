using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SheepAI.Application.Interfaces.ExternalServices;
using StackExchange.Redis;

namespace SheepAI.Infrastructure.ExternalServices;

public sealed class CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger) : ICacheService
{
    private static readonly ConcurrentDictionary<string, Task<byte[]>> _inflight = new();

    private IDatabase Db => redis.GetDatabase();

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(key);
        if (value.IsNull)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Cache miss for key '{CacheKey}'.", key);
            return default;
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Cache hit for key '{CacheKey}'.", key);
        return JsonSerializer.Deserialize<T>((byte[])value!);
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Setting cache for key '{CacheKey}' with TTL {TTL}.", key, ttl);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await Db.StringSetAsync(key, bytes, ttl);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Removing cache key '{CacheKey}'.", key);
        await Db.KeyDeleteAsync(key);
    }

    /// <inheritdoc/>
    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        var server = redis.GetServers().First();
        var keys = server.KeysAsync(pattern: pattern);
        await foreach (var key in keys.WithCancellation(ct))
            await Db.KeyDeleteAsync(key);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Removed cache keys matching pattern '{Pattern}'.", pattern);
    }

    /// <inheritdoc/>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(key);
        if (!value.IsNull)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Cache hit for key '{CacheKey}'.", key);
            return JsonSerializer.Deserialize<T>((byte[])value!)!;
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Cache miss for key '{CacheKey}'.", key);

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var shared = _inflight.GetOrAdd(key, tcs.Task);

        if (!ReferenceEquals(shared, tcs.Task))
            return JsonSerializer.Deserialize<T>(await shared)!;

        return await PopulateAsync(tcs, key, factory, ttl);
    }

    private async Task<T> PopulateAsync<T>(TaskCompletionSource<byte[]> tcs, string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        try
        {
            var value = await factory();
            var serialized = JsonSerializer.SerializeToUtf8Bytes(value);
            await Db.StringSetAsync(key, serialized, ttl);
            tcs.SetResult(serialized);
            return value;
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }
}
