using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Node.Runtime.Configuration;
using Node.Runtime.Services;
using Xunit;

namespace Node.Runtime.Tests.Services;

/// <summary>
/// Tests for NodeMetricsService
/// </summary>
public class NodeMetricsServiceTests
{
    private readonly Mock<ILogger<NodeMetricsService>> _mockLogger;
    private readonly NodeRuntimeOptions _options;

    public NodeMetricsServiceTests()
    {
        _mockLogger = new Mock<ILogger<NodeMetricsService>>();
        _options = new NodeRuntimeOptions
        {
            NodeId = "test-node",
            MaxConcurrentLeases = 5
        };
    }

    [Fact]
    public void GetActiveLeases_Initially_ReturnsZero()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);

        // Act
        var result = service.GetActiveLeases();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetAvailableSlots_Initially_ReturnsMaxConcurrentLeases()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);

        // Act
        var result = service.GetAvailableSlots();

        // Assert
        Assert.Equal(_options.MaxConcurrentLeases, result);
    }

    [Fact]
    public void IncrementActiveLeases_IncreasesCount()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);

        // Act
        service.IncrementActiveLeases();
        var result = service.GetActiveLeases();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void DecrementActiveLeases_DecreasesCount()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();

        // Act
        service.DecrementActiveLeases();
        var result = service.GetActiveLeases();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetAvailableSlots_AfterIncrement_ReturnsCorrectAvailableSlots()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();

        // Act
        var result = service.GetAvailableSlots();

        // Assert
        Assert.Equal(2, result); // 5 max - 3 active = 2 available
    }

    [Fact]
    public void GetAvailableSlots_WhenAllSlotsUsed_ReturnsZero()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);
        for (int i = 0; i < 5; i++)
        {
            service.IncrementActiveLeases();
        }

        // Act
        var result = service.GetAvailableSlots();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetAvailableSlots_WhenActiveLeasesExceedsMax_ReturnsZero()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);

        // Simulate an edge case where active leases somehow exceeds max
        for (int i = 0; i < 7; i++)
        {
            service.IncrementActiveLeases();
        }

        // Act
        var result = service.GetAvailableSlots();

        // Assert
        Assert.Equal(0, result); // Should not return negative
    }

    [Fact]
    public async Task IncrementAndDecrement_ThreadSafe()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);
        const int iterations = 1000;

        // Act - Simulate concurrent increments and decrements
        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(() => service.IncrementActiveLeases()));
            tasks.Add(Task.Run(() => service.DecrementActiveLeases()));
        }
        await Task.WhenAll(tasks.ToArray());

        // Assert - Should be back to zero
        Assert.Equal(0, service.GetActiveLeases());
    }

    [Fact]
    public void GetActiveLeases_MultipleIncrements_ReturnsCorrectCount()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);

        // Act
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();
        var result = service.GetActiveLeases();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetActiveLeases_AfterDecrementToZero_ReturnsZero()
    {
        // Arrange
        var options = Options.Create(_options);
        var service = new NodeMetricsService(options, _mockLogger.Object);
        service.IncrementActiveLeases();
        service.IncrementActiveLeases();

        // Act
        service.DecrementActiveLeases();
        service.DecrementActiveLeases();
        var result = service.GetActiveLeases();

        // Assert
        Assert.Equal(0, result);
    }
}
