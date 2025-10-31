using StackExchange.Redis;

namespace ControlPlane.Api.Services;

/// <summary>
/// Redis-backed implementation of ILockStore using TTL expiry.
/// Uses Redis SET NX (set if not exists) for atomic lock acquisition.
/// </summary>
public class RedisLockStore : ILockStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private const string LockKeyPrefix = "lock:";

    public RedisLockStore(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
    }

    public async Task<bool> AcquireLockAsync(string lockKey, string ownerId, int ttlSeconds)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("LockKey cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("OwnerId cannot be null or empty", nameof(ownerId));
        if (ttlSeconds <= 0)
            throw new ArgumentException("TTL must be greater than 0", nameof(ttlSeconds));

        var key = GetLockKey(lockKey);
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        // Use SET NX (set if not exists) to ensure atomic lock acquisition
        return await _db.StringSetAsync(key, ownerId, ttl, When.NotExists);
    }

    public async Task<bool> ReleaseLockAsync(string lockKey, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("LockKey cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("OwnerId cannot be null or empty", nameof(ownerId));

        var key = GetLockKey(lockKey);

        // Use Lua script to ensure atomic check-and-delete
        // Only delete if the owner matches
        const string script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { ownerId });
        return (int)result == 1;
    }

    public async Task<bool> IsLockedAsync(string lockKey)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("LockKey cannot be null or empty", nameof(lockKey));

        var key = GetLockKey(lockKey);
        return await _db.KeyExistsAsync(key);
    }

    public async Task<bool> ExtendLockAsync(string lockKey, string ownerId, int additionalSeconds)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("LockKey cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("OwnerId cannot be null or empty", nameof(ownerId));
        if (additionalSeconds <= 0)
            throw new ArgumentException("Additional seconds must be greater than 0", nameof(additionalSeconds));

        var key = GetLockKey(lockKey);

        // Use Lua script to ensure atomic check-and-extend
        // Only extend if the owner matches
        const string script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                local ttl = redis.call('ttl', KEYS[1])
                if ttl > 0 then
                    return redis.call('expire', KEYS[1], ttl + tonumber(ARGV[2]))
                else
                    return 0
                end
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { key },
            new RedisValue[] { ownerId, additionalSeconds });

        return (int)result == 1;
    }

    private static string GetLockKey(string lockKey) => $"{LockKeyPrefix}{lockKey}";
}
