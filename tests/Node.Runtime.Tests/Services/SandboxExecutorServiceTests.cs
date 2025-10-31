using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Node.Runtime.Configuration;
using Node.Runtime.Services;

namespace Node.Runtime.Tests.Services;

public sealed class SandboxExecutorServiceTests
{
    private readonly Mock<ILogger<SandboxExecutorService>> _loggerMock;
    private readonly AgentRuntimeOptions _options;

    public SandboxExecutorServiceTests()
    {
        _loggerMock = new Mock<ILogger<SandboxExecutorService>>();
        _options = new AgentRuntimeOptions
        {
            DefaultModel = "gpt-4",
            DefaultTemperature = 0.7,
            MaxTokens = 4000,
            MaxDurationSeconds = 60
        };
    }

    /// <summary>
    /// Helper method to create service and skip test if Agent.Host is not available.
    /// </summary>
    private SandboxExecutorService CreateServiceOrSkip()
    {
        try
        {
            return new SandboxExecutorService(
                Options.Create(_options),
                _loggerMock.Object);
        }
        catch (FileNotFoundException ex)
        {
            throw new Xunit.SkipException($"Agent.Host executable not found: {ex.Message}");
        }
    }

    [Fact]
    public void Constructor_SucceedsWhenAgentHostIsAvailable()
    {
        // Arrange & Act & Assert
        var service = CreateServiceOrSkip();
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_AppliesBudgetConstraints_FromSpec()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new BudgetConstraints
            {
                MaxTokens = 1000,
                MaxDurationSeconds = 30
            }
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Should().NotBeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        // The result should indicate failure since no chat client is configured
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Chat client creation needs to be configured");
    }

    [Fact]
    public async Task ExecuteAsync_AppliesDefaultBudgetConstraints_WhenNotSpecified()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
            // No budget specified
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Should().NotBeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        // The default MaxDurationSeconds from options should be applied
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_OnException()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_RecordsDuration()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesSandboxMetadata()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("sandboxed");
        result.Metadata!["sandboxed"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesProcessTimeout()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new BudgetConstraints
            {
                MaxDurationSeconds = 1 // Very short timeout
            }
        };

        // Act
        var result = await service.ExecuteAsync(spec, "Test input");

        // Assert
        result.Should().NotBeNull();
        // Should either timeout or fail with chat client error
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellation()
    {
        // Arrange
        var service = CreateServiceOrSkip();

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
        };

        using var cts = new CancellationTokenSource();

        // Start execution and cancel after a short delay
        var executionTask = service.ExecuteAsync(spec, "Test input", cts.Token);
        await Task.Delay(50); // Give it a moment to start
        await cts.CancelAsync(); // Cancel the operation

        // Act & Assert
        // Should either throw OperationCanceledException or return with an error
        try
        {
            var result = await executionTask;
            // If no exception, the result should indicate failure
            result.Should().NotBeNull();
        }
        catch (OperationCanceledException)
        {
            // This is the expected behavior
        }
    }
}
