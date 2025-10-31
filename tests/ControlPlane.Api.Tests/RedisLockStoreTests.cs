using ControlPlane.Api.Services;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ControlPlane.Api.Tests;

public class RedisLockStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _redis;
    private RedisLockStore? _store;

    public RedisLockStoreTests()
    {
        _redisContainer = new RedisBuilder().Build();
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        _store = new RedisLockStore(_redis);
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLockAsync_SuccessfullyAcquiresNewLock()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "owner-1";
        var ttlSeconds = 30;

        // Act
        var result = await _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds);

        // Assert
        Assert.True(result);
        Assert.True(await _store.IsLockedAsync(lockKey));
    }

    [Fact]
    public async Task AcquireLockAsync_ReturnsFalse_WhenLockAlreadyHeld()
    {
        // Arrange
        var lockKey = "resource-2";
        var owner1 = "owner-1";
        var owner2 = "owner-2";
        var ttlSeconds = 30;

        // Act
        var firstAcquire = await _store!.AcquireLockAsync(lockKey, owner1, ttlSeconds);
        var secondAcquire = await _store.AcquireLockAsync(lockKey, owner2, ttlSeconds);

        // Assert
        Assert.True(firstAcquire);
        Assert.False(secondAcquire);
    }

    [Fact]
    public async Task AcquireLockAsync_ThrowsException_WhenLockKeyIsEmpty()
    {
        // Arrange
        var lockKey = "";
        var ownerId = "owner-1";
        var ttlSeconds = 30;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds));
    }

    [Fact]
    public async Task AcquireLockAsync_ThrowsException_WhenOwnerIdIsEmpty()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "";
        var ttlSeconds = 30;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds));
    }

    [Fact]
    public async Task AcquireLockAsync_ThrowsException_WhenTtlIsZeroOrNegative()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "owner-1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.AcquireLockAsync(lockKey, ownerId, 0));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.AcquireLockAsync(lockKey, ownerId, -1));
    }

    [Fact]
    public async Task ReleaseLockAsync_SuccessfullyReleasesLock_WhenOwnerMatches()
    {
        // Arrange
        var lockKey = "resource-3";
        var ownerId = "owner-1";
        var ttlSeconds = 30;
        await _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds);

        // Act
        var result = await _store.ReleaseLockAsync(lockKey, ownerId);

        // Assert
        Assert.True(result);
        Assert.False(await _store.IsLockedAsync(lockKey));
    }

    [Fact]
    public async Task ReleaseLockAsync_ReturnsFalse_WhenOwnerDoesNotMatch()
    {
        // Arrange
        var lockKey = "resource-4";
        var owner1 = "owner-1";
        var owner2 = "owner-2";
        var ttlSeconds = 30;
        await _store!.AcquireLockAsync(lockKey, owner1, ttlSeconds);

        // Act
        var result = await _store.ReleaseLockAsync(lockKey, owner2);

        // Assert
        Assert.False(result);
        Assert.True(await _store.IsLockedAsync(lockKey)); // Lock still held by owner1
    }

    [Fact]
    public async Task ReleaseLockAsync_ReturnsFalse_WhenLockDoesNotExist()
    {
        // Arrange
        var lockKey = "nonexistent-lock";
        var ownerId = "owner-1";

        // Act
        var result = await _store!.ReleaseLockAsync(lockKey, ownerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseLockAsync_ThrowsException_WhenLockKeyIsEmpty()
    {
        // Arrange
        var lockKey = "";
        var ownerId = "owner-1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ReleaseLockAsync(lockKey, ownerId));
    }

    [Fact]
    public async Task ReleaseLockAsync_ThrowsException_WhenOwnerIdIsEmpty()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ReleaseLockAsync(lockKey, ownerId));
    }

    [Fact]
    public async Task IsLockedAsync_ReturnsTrue_WhenLockExists()
    {
        // Arrange
        var lockKey = "resource-5";
        var ownerId = "owner-1";
        var ttlSeconds = 30;
        await _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds);

        // Act
        var result = await _store.IsLockedAsync(lockKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsLockedAsync_ReturnsFalse_WhenLockDoesNotExist()
    {
        // Arrange
        var lockKey = "nonexistent-lock";

        // Act
        var result = await _store!.IsLockedAsync(lockKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsLockedAsync_ThrowsException_WhenLockKeyIsEmpty()
    {
        // Arrange
        var lockKey = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.IsLockedAsync(lockKey));
    }

    [Fact]
    public async Task LockExpires_AfterTtl()
    {
        // Arrange
        var lockKey = "resource-expire";
        var ownerId = "owner-1";
        var ttlSeconds = 2; // Short TTL for testing
        await _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds);

        // Act - Wait for TTL to expire
        await Task.Delay(TimeSpan.FromSeconds(ttlSeconds + 1));
        var isLocked = await _store.IsLockedAsync(lockKey);

        // Assert
        Assert.False(isLocked); // Lock should have expired
    }

    [Fact]
    public async Task ExtendLockAsync_SuccessfullyExtendsLock_WhenOwnerMatches()
    {
        // Arrange
        var lockKey = "resource-extend";
        var ownerId = "owner-1";
        var initialTtl = 5;
        var additionalSeconds = 10;
        await _store!.AcquireLockAsync(lockKey, ownerId, initialTtl);

        // Act
        var result = await _store.ExtendLockAsync(lockKey, ownerId, additionalSeconds);

        // Assert
        Assert.True(result);
        Assert.True(await _store.IsLockedAsync(lockKey));
    }

    [Fact]
    public async Task ExtendLockAsync_ReturnsFalse_WhenOwnerDoesNotMatch()
    {
        // Arrange
        var lockKey = "resource-extend-2";
        var owner1 = "owner-1";
        var owner2 = "owner-2";
        var initialTtl = 30;
        var additionalSeconds = 10;
        await _store!.AcquireLockAsync(lockKey, owner1, initialTtl);

        // Act
        var result = await _store.ExtendLockAsync(lockKey, owner2, additionalSeconds);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendLockAsync_ReturnsFalse_WhenLockDoesNotExist()
    {
        // Arrange
        var lockKey = "nonexistent-lock";
        var ownerId = "owner-1";
        var additionalSeconds = 10;

        // Act
        var result = await _store!.ExtendLockAsync(lockKey, ownerId, additionalSeconds);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendLockAsync_ThrowsException_WhenLockKeyIsEmpty()
    {
        // Arrange
        var lockKey = "";
        var ownerId = "owner-1";
        var additionalSeconds = 10;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ExtendLockAsync(lockKey, ownerId, additionalSeconds));
    }

    [Fact]
    public async Task ExtendLockAsync_ThrowsException_WhenOwnerIdIsEmpty()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "";
        var additionalSeconds = 10;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ExtendLockAsync(lockKey, ownerId, additionalSeconds));
    }

    [Fact]
    public async Task ExtendLockAsync_ThrowsException_WhenAdditionalSecondsIsZeroOrNegative()
    {
        // Arrange
        var lockKey = "resource-1";
        var ownerId = "owner-1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ExtendLockAsync(lockKey, ownerId, 0));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.ExtendLockAsync(lockKey, ownerId, -1));
    }

    [Fact]
    public async Task SameOwner_CanReacquireLock_AfterRelease()
    {
        // Arrange
        var lockKey = "resource-reacquire";
        var ownerId = "owner-1";
        var ttlSeconds = 30;
        await _store!.AcquireLockAsync(lockKey, ownerId, ttlSeconds);
        await _store.ReleaseLockAsync(lockKey, ownerId);

        // Act
        var result = await _store.AcquireLockAsync(lockKey, ownerId, ttlSeconds);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DifferentOwner_CanAcquireLock_AfterRelease()
    {
        // Arrange
        var lockKey = "resource-handoff";
        var owner1 = "owner-1";
        var owner2 = "owner-2";
        var ttlSeconds = 30;
        await _store!.AcquireLockAsync(lockKey, owner1, ttlSeconds);
        await _store.ReleaseLockAsync(lockKey, owner1);

        // Act
        var result = await _store.AcquireLockAsync(lockKey, owner2, ttlSeconds);

        // Assert
        Assert.True(result);
    }
}
