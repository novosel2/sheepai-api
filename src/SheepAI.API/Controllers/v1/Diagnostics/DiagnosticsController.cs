using Microsoft.AspNetCore.Mvc;
using SheepAI.API.Common;
using SheepAI.Application.Interfaces.ExternalServices;

namespace SheepAI.API.Controllers.v1.Diagnostics;

/// <summary>Internal diagnostics endpoints for verifying infrastructure connectivity.</summary>
[ApiController]
[Route("api/v1/diagnostics")]
public sealed class DiagnosticsController(ICacheService cacheService, ILogger<DiagnosticsController> logger) : ControllerBase
{
    private const string TestKey = "diagnostics:cache-test";

    /// <summary>Writes a value to Redis and reads it back to verify cache connectivity.</summary>
    /// <remarks>Returns the round-trip duration and confirms the read value matches the written value.</remarks>
    [HttpGet("cache")]
    [ProducesResponseType<ApiResponse<CacheTestResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CacheTest(CancellationToken cancellationToken)
    {
        var expected = Guid.NewGuid().ToString("N");
        var ttl = TimeSpan.FromSeconds(30);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await cacheService.SetAsync(TestKey, expected, ttl, cancellationToken);
        var actual = await cacheService.GetAsync<string>(TestKey, cancellationToken);
        sw.Stop();

        await cacheService.RemoveAsync(TestKey, cancellationToken);

        var ok = actual == expected;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Cache test: written={Expected} read={Actual} match={Ok} elapsed={ElapsedMs}ms",
                expected, actual, ok, sw.ElapsedMilliseconds);

        var result = new CacheTestResult(ok, sw.ElapsedMilliseconds);
        return Ok(Api.Data(ok ? "Cache is reachable." : "Cache read/write mismatch.", result));
    }

    private ILogger<DiagnosticsController> _logger => logger;
}

/// <summary>Result of a cache connectivity test.</summary>
/// <param name="Success">Whether the value written to Redis was successfully read back.</param>
/// <param name="RoundTripMs">Total elapsed milliseconds for the write + read operations.</param>
public sealed record CacheTestResult(bool Success, long RoundTripMs);
