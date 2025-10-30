namespace ControlPlane.Api.Services;

/// <summary>
/// Store for managing distributed locks with TTL expiry.
/// Used for coordinating operations across multiple control plane instances.
/// </summary>
public interface ILockStore
{
    /// <summary>
    /// Attempt to acquire a distributed lock with a TTL.
    /// Returns true if the lock was acquired, false if it's already held.
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock</param>
    /// <param name="ownerId">Identifier for the lock owner (e.g., process ID)</param>
    /// <param name="ttlSeconds">Time-to-live in seconds for the lock</param>
    /// <returns>True if lock was acquired, false if already held by another owner</returns>
    Task<bool> AcquireLockAsync(string lockKey, string ownerId, int ttlSeconds);
    
    /// <summary>
    /// Release a lock, allowing it to be acquired by another owner.
    /// Only succeeds if the current owner matches.
    /// </summary>
    /// <param name="lockKey">The lock key to release</param>
    /// <param name="ownerId">The owner ID attempting to release the lock</param>
    /// <returns>True if lock was released, false if it didn't exist or owner didn't match</returns>
    Task<bool> ReleaseLockAsync(string lockKey, string ownerId);
    
    /// <summary>
    /// Check if a lock is currently held.
    /// </summary>
    /// <param name="lockKey">The lock key to check</param>
    /// <returns>True if the lock is held, false otherwise</returns>
    Task<bool> IsLockedAsync(string lockKey);
    
    /// <summary>
    /// Extend an existing lock by adding more time to its TTL.
    /// Only succeeds if the current owner matches.
    /// </summary>
    /// <param name="lockKey">The lock key to extend</param>
    /// <param name="ownerId">The owner ID attempting to extend the lock</param>
    /// <param name="additionalSeconds">Additional seconds to add to the TTL</param>
    /// <returns>True if the lock was extended, false if it doesn't exist or owner didn't match</returns>
    Task<bool> ExtendLockAsync(string lockKey, string ownerId, int additionalSeconds);
}
