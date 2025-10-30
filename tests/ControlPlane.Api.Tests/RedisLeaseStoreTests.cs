using ControlPlane.Api.Services;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ControlPlane.Api.Tests;

public class RedisLeaseStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _redis;
    private RedisLeaseStore? _store;

    public RedisLeaseStoreTests()
    {
        _redisContainer = new RedisBuilder().Build();
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        _store = new RedisLeaseStore(_redis);
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task AcquireLeaseAsync_SuccessfullyAcquiresNewLease()
    {
        // Arrange
        var runId = "run-123";
        var nodeId = "node-1";
        var ttlSeconds = 30;

        // Act
        var result = await _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds);

        // Assert
        Assert.True(result);
        
        var lease = await _store.GetLeaseAsync(runId);
        Assert.NotNull(lease);
        Assert.Equal(runId, lease.RunId);
        Assert.Equal(nodeId, lease.NodeId);
    }

    [Fact]
    public async Task AcquireLeaseAsync_ReturnsFalse_WhenLeaseAlreadyExists()
    {
        // Arrange
        var runId = "run-456";
        var nodeId1 = "node-1";
        var nodeId2 = "node-2";
        var ttlSeconds = 30;

        // Act
        var firstAcquire = await _store!.AcquireLeaseAsync(runId, nodeId1, ttlSeconds);
        var secondAcquire = await _store.AcquireLeaseAsync(runId, nodeId2, ttlSeconds);

        // Assert
        Assert.True(firstAcquire);
        Assert.False(secondAcquire);
        
        var lease = await _store.GetLeaseAsync(runId);
        Assert.NotNull(lease);
        Assert.Equal(nodeId1, lease.NodeId); // Original node still holds the lease
    }

    [Fact]
    public async Task AcquireLeaseAsync_ThrowsException_WhenRunIdIsEmpty()
    {
        // Arrange
        var runId = "";
        var nodeId = "node-1";
        var ttlSeconds = 30;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds));
    }

    [Fact]
    public async Task AcquireLeaseAsync_ThrowsException_WhenNodeIdIsEmpty()
    {
        // Arrange
        var runId = "run-123";
        var nodeId = "";
        var ttlSeconds = 30;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds));
    }

    [Fact]
    public async Task AcquireLeaseAsync_ThrowsException_WhenTtlIsZeroOrNegative()
    {
        // Arrange
        var runId = "run-123";
        var nodeId = "node-1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.AcquireLeaseAsync(runId, nodeId, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.AcquireLeaseAsync(runId, nodeId, -1));
    }

    [Fact]
    public async Task ReleaseLeaseAsync_SuccessfullyReleasesLease()
    {
        // Arrange
        var runId = "run-789";
        var nodeId = "node-1";
        var ttlSeconds = 30;
        await _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds);

        // Act
        var result = await _store.ReleaseLeaseAsync(runId);

        // Assert
        Assert.True(result);
        
        var lease = await _store.GetLeaseAsync(runId);
        Assert.Null(lease);
    }

    [Fact]
    public async Task ReleaseLeaseAsync_ReturnsFalse_WhenLeaseDoesNotExist()
    {
        // Arrange
        var runId = "nonexistent-run";

        // Act
        var result = await _store!.ReleaseLeaseAsync(runId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseLeaseAsync_ThrowsException_WhenRunIdIsEmpty()
    {
        // Arrange
        var runId = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.ReleaseLeaseAsync(runId));
    }

    [Fact]
    public async Task GetLeaseAsync_ReturnsLease_WhenExists()
    {
        // Arrange
        var runId = "run-abc";
        var nodeId = "node-1";
        var ttlSeconds = 30;
        await _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds);

        // Act
        var lease = await _store.GetLeaseAsync(runId);

        // Assert
        Assert.NotNull(lease);
        Assert.Equal(runId, lease.RunId);
        Assert.Equal(nodeId, lease.NodeId);
        Assert.True(lease.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task GetLeaseAsync_ReturnsNull_WhenDoesNotExist()
    {
        // Arrange
        var runId = "nonexistent-run";

        // Act
        var lease = await _store!.GetLeaseAsync(runId);

        // Assert
        Assert.Null(lease);
    }

    [Fact]
    public async Task GetLeaseAsync_ThrowsException_WhenRunIdIsEmpty()
    {
        // Arrange
        var runId = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.GetLeaseAsync(runId));
    }

    [Fact]
    public async Task LeaseExpires_AfterTtl()
    {
        // Arrange
        var runId = "run-expire";
        var nodeId = "node-1";
        var ttlSeconds = 2; // Short TTL for testing
        await _store!.AcquireLeaseAsync(runId, nodeId, ttlSeconds);

        // Act - Wait for TTL to expire
        await Task.Delay(TimeSpan.FromSeconds(ttlSeconds + 1));
        var lease = await _store.GetLeaseAsync(runId);

        // Assert
        Assert.Null(lease); // Lease should have expired
    }

    [Fact]
    public async Task ExtendLeaseAsync_SuccessfullyExtendsLease()
    {
        // Arrange
        var runId = "run-extend";
        var nodeId = "node-1";
        var initialTtl = 5;
        var additionalSeconds = 10;
        await _store!.AcquireLeaseAsync(runId, nodeId, initialTtl);
        
        var leaseBefore = await _store.GetLeaseAsync(runId);
        Assert.NotNull(leaseBefore);

        // Act
        var result = await _store.ExtendLeaseAsync(runId, additionalSeconds);

        // Assert
        Assert.True(result);
        
        var leaseAfter = await _store.GetLeaseAsync(runId);
        Assert.NotNull(leaseAfter);
        Assert.True(leaseAfter.ExpiresAt > leaseBefore.ExpiresAt);
    }

    [Fact]
    public async Task ExtendLeaseAsync_ReturnsFalse_WhenLeaseDoesNotExist()
    {
        // Arrange
        var runId = "nonexistent-run";
        var additionalSeconds = 10;

        // Act
        var result = await _store!.ExtendLeaseAsync(runId, additionalSeconds);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtendLeaseAsync_ThrowsException_WhenRunIdIsEmpty()
    {
        // Arrange
        var runId = "";
        var additionalSeconds = 10;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.ExtendLeaseAsync(runId, additionalSeconds));
    }

    [Fact]
    public async Task ExtendLeaseAsync_ThrowsException_WhenAdditionalSecondsIsZeroOrNegative()
    {
        // Arrange
        var runId = "run-123";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.ExtendLeaseAsync(runId, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _store!.ExtendLeaseAsync(runId, -1));
    }
}
