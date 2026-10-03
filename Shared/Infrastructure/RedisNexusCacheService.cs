using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace NEXUS.Shared.Infrastructure;

public interface INexusCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task InvalidateEnterpriseStateAsync(CancellationToken cancellationToken = default);
}

public static class NexusCacheKeys
{
    public const string EnterpriseState = "nexus:state:current";
    public const string DigitalTwinSnapshot = "nexus:twin:snapshot";
    public const string DependencyGraph = "nexus:graph:full";
    public const string DashboardMetrics = "nexus:dashboard:metrics";
    public const string ResilienceScore = "nexus:resilience:current";
    public static string JobStatus(Guid jobId) => $"nexus:job:{jobId}";
    public static string ScenarioTemp(Guid scenarioId) => $"nexus:scenario:{scenarioId}";
}

public sealed class RedisNexusCacheService : INexusCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisNexusCacheService> _logger;
    private readonly ConcurrentDictionary<string, (string Json, DateTime ExpiresAtUtc)> _fallbackStore = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RedisNexusCacheService(
        IDistributedCache distributedCache,
        ILogger<RedisNexusCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(payload))
            {
                return JsonSerializer.Deserialize<T>(payload, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Distributed cache read fallback for key {CacheKey}", key);
        }

        if (_fallbackStore.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAtUtc > DateTime.UtcNow)
            {
                return JsonSerializer.Deserialize<T>(entry.Json, JsonOptions);
            }

            _fallbackStore.TryRemove(key, out _);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        _fallbackStore[key] = (json, DateTime.UtcNow.Add(ttl));

        try
        {
            await _distributedCache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Distributed cache write fallback for key {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _fallbackStore.TryRemove(key, out _);
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Distributed cache remove fallback for key {CacheKey}", key);
        }
    }

    public async Task InvalidateEnterpriseStateAsync(CancellationToken cancellationToken = default)
    {
        await RemoveAsync(NexusCacheKeys.EnterpriseState, cancellationToken);
        await RemoveAsync(NexusCacheKeys.DigitalTwinSnapshot, cancellationToken);
        await RemoveAsync(NexusCacheKeys.DependencyGraph, cancellationToken);
        await RemoveAsync(NexusCacheKeys.DashboardMetrics, cancellationToken);
        await RemoveAsync(NexusCacheKeys.ResilienceScore, cancellationToken);
    }
}
