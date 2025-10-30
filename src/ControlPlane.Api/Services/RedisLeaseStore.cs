using StackExchange.Redis;
using System.Text.Json;

namespace ControlPlane.Api.Services;

/// <summary>
/// Redis-backed implementation of ILeaseStore using TTL expiry.
/// Stores leases as Redis keys with automatic expiration.
/// </summary>
public class RedisLeaseStore : ILeaseStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private const string LeaseKeyPrefix = "lease:";

    public RedisLeaseStore(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
    }

    public async Task<bool> AcquireLeaseAsync(string runId, string nodeId, int ttlSeconds)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId cannot be null or empty", nameof(runId));
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("NodeId cannot be null or empty", nameof(nodeId));
        if (ttlSeconds <= 0)
            throw new ArgumentException("TTL must be greater than 0", nameof(ttlSeconds));

        var key = GetLeaseKey(runId);
        var lease = new Lease
        {
            RunId = runId,
            NodeId = nodeId,
            AcquiredAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds)
        };

        var value = JsonSerializer.Serialize(lease);
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        // Use SET NX (set if not exists) to ensure atomic acquisition
        return await _db.StringSetAsync(key, value, ttl, When.NotExists);
    }

    public async Task<bool> ReleaseLeaseAsync(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId cannot be null or empty", nameof(runId));

        var key = GetLeaseKey(runId);
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<Lease?> GetLeaseAsync(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId cannot be null or empty", nameof(runId));

        var key = GetLeaseKey(runId);
        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return null;

        var lease = JsonSerializer.Deserialize<Lease>(value.ToString());
        
        // Double-check expiration (Redis should auto-expire, but defensive check)
        if (lease != null && lease.ExpiresAt < DateTime.UtcNow)
            return null;

        return lease;
    }

    public async Task<bool> ExtendLeaseAsync(string runId, int additionalSeconds)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId cannot be null or empty", nameof(runId));
        if (additionalSeconds <= 0)
            throw new ArgumentException("Additional seconds must be greater than 0", nameof(additionalSeconds));

        var key = GetLeaseKey(runId);
        
        // Get current lease to update expiry time
        var lease = await GetLeaseAsync(runId);
        if (lease == null)
            return false;

        // Update the expiry time
        lease.ExpiresAt = lease.ExpiresAt.AddSeconds(additionalSeconds);
        
        // Get current TTL and add additional time
        var currentTtl = await _db.KeyTimeToLiveAsync(key);
        if (!currentTtl.HasValue)
            return false;

        var newTtl = currentTtl.Value.Add(TimeSpan.FromSeconds(additionalSeconds));
        
        // Update both the value and the TTL
        var value = JsonSerializer.Serialize(lease);
        await _db.StringSetAsync(key, value, newTtl);
        
        return true;
    }

    private static string GetLeaseKey(string runId) => $"{LeaseKeyPrefix}{runId}";
}
