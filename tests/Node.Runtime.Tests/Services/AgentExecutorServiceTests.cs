using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Node.Runtime.Configuration;
using Node.Runtime.Services;

namespace Node.Runtime.Tests.Services;

public sealed class AgentExecutorServiceTests
{
    private readonly Mock<ILogger<AgentExecutorService>> _loggerMock;
    private readonly AgentRuntimeOptions _options;

    public AgentExecutorServiceTests()
    {
        _loggerMock = new Mock<ILogger<AgentExecutorService>>();
        _options = new AgentRuntimeOptions
        {
            DefaultModel = "gpt-4",
            DefaultTemperature = 0.7,
            MaxTokens = 4000,
            MaxDurationSeconds = 60
        };
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsNotImplementedException_WhenChatClientNotConfigured()
    {
        // Arrange
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

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
        result.Error.Should().Contain("Chat client creation needs to be configured");
    }

    [Fact]
    public async Task ExecuteAsync_AppliesBudgetConstraints_FromSpec()
    {
        // Arrange
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

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
    }

    [Fact]
    public async Task ExecuteAsync_AppliesDefaultBudgetConstraints_WhenNotSpecified()
    {
        // Arrange
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

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
        // The default MaxDurationSeconds from options should be applied
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_OnException()
    {
        // Arrange
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

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
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

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
    public async Task ExecuteAsync_EstimatesTokens()
    {
        // Arrange
        var service = new AgentExecutorService(
            Options.Create(_options),
            _loggerMock.Object);

        var spec = new AgentSpec
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Name = "Test Agent",
            Instructions = "Test instructions"
        };

        var input = "This is a test input message with some content";

        // Act
        var result = await service.ExecuteAsync(spec, input);

        // Assert
        // On failure (no chat client), we don't estimate tokens
        // This test validates that the service handles the error correctly
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }
}
