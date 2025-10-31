using ControlPlane.Api.Data;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Unit tests for PostgresRunStore ensuring run lifecycle management and metrics tracking works correctly.
/// </summary>
public class PostgresRunStoreTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PostgresRunStore _store;

    public PostgresRunStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _store = new PostgresRunStore(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateRunAsync Tests

    [Fact]
    public async Task CreateRunAsync_WithValidParameters_CreatesRun()
    {
        // Act
        var result = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Assert
        result.Should().NotBeNull();
        result.RunId.Should().NotBeNullOrEmpty();
        result.AgentId.Should().Be("agent-1");
        result.Version.Should().Be("1.0.0");
        result.Status.Should().Be("pending");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateRunAsync_GeneratesUniqueRunIds()
    {
        // Act
        var run1 = await _store.CreateRunAsync("agent-1", "1.0.0");
        var run2 = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Assert
        run1.RunId.Should().NotBe(run2.RunId);
    }

    #endregion

    #region GetRunAsync Tests

    [Fact]
    public async Task GetRunAsync_WithExistingRun_ReturnsRun()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Act
        var result = await _store.GetRunAsync(run.RunId);

        // Assert
        result.Should().NotBeNull();
        result!.RunId.Should().Be(run.RunId);
        result.AgentId.Should().Be("agent-1");
    }

    [Fact]
    public async Task GetRunAsync_WithNonExistentRun_ReturnsNull()
    {
        // Act
        var result = await _store.GetRunAsync("non-existent-run");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllRunsAsync Tests

    [Fact]
    public async Task GetAllRunsAsync_WithNoRuns_ReturnsEmptyList()
    {
        // Act
        var result = await _store.GetAllRunsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRunsAsync_WithMultipleRuns_ReturnsAllRuns()
    {
        // Arrange
        await _store.CreateRunAsync("agent-1", "1.0.0");
        await _store.CreateRunAsync("agent-2", "2.0.0");
        await _store.CreateRunAsync("agent-1", "1.1.0");

        // Act
        var result = await _store.GetAllRunsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    #endregion

    #region CompleteRunAsync Tests

    [Fact]
    public async Task CompleteRunAsync_WithExistingRun_UpdatesStatusToCompleted()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        var completeRequest = new CompleteRunRequest
        {
            Timings = new Dictionary<string, object>
            {
                ["duration"] = 1500.5,
                ["llm_time"] = 1200.0
            },
            Costs = new Dictionary<string, object>
            {
                ["tokens"] = 1000,
                ["usd"] = 0.15
            }
        };

        // Act
        var result = await _store.CompleteRunAsync(run.RunId, completeRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("completed");
        result.Timings.Should().NotBeNull();
        result.Timings!.Should().ContainKey("duration");
        result.Timings.Should().ContainKey("llm_time");
        result.Costs.Should().NotBeNull();
        result.Costs!.Should().ContainKey("tokens");
        result.Costs.Should().ContainKey("usd");
    }

    [Fact]
    public async Task CompleteRunAsync_WithNonExistentRun_ReturnsNull()
    {
        // Arrange
        var completeRequest = new CompleteRunRequest();

        // Act
        var result = await _store.CompleteRunAsync("non-existent-run", completeRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CompleteRunAsync_WithMinimalData_CompletesSuccessfully()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");
        var completeRequest = new CompleteRunRequest();

        // Act
        var result = await _store.CompleteRunAsync(run.RunId, completeRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("completed");
    }

    #endregion

    #region FailRunAsync Tests

    [Fact]
    public async Task FailRunAsync_WithExistingRun_UpdatesStatusToFailed()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        var failRequest = new FailRunRequest
        {
            ErrorMessage = "LLM timeout",
            ErrorDetails = "Error code: TIMEOUT, Retry count: 3",
            Timings = new Dictionary<string, object>
            {
                ["duration"] = 60000.0
            }
        };

        // Act
        var result = await _store.FailRunAsync(run.RunId, failRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("failed");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Should().ContainKey("errorMessage");
        result.Timings.Should().NotBeNull();
    }

    [Fact]
    public async Task FailRunAsync_WithNonExistentRun_ReturnsNull()
    {
        // Arrange
        var failRequest = new FailRunRequest { ErrorMessage = "Error" };

        // Act
        var result = await _store.FailRunAsync("non-existent-run", failRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FailRunAsync_WithoutErrorDetails_StoresErrorMessageOnly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");
        var failRequest = new FailRunRequest { ErrorMessage = "Simple error" };

        // Act
        var result = await _store.FailRunAsync(run.RunId, failRequest);

        // Assert
        result.Should().NotBeNull();
        result!.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Should().ContainKey("errorMessage");
        result.ErrorInfo.Should().NotContainKey("errorDetails");
    }

    #endregion

    #region CancelRunAsync Tests

    [Fact]
    public async Task CancelRunAsync_WithExistingRun_UpdatesStatusToCancelled()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        var cancelRequest = new CancelRunRequest
        {
            Reason = "User requested cancellation"
        };

        // Act
        var result = await _store.CancelRunAsync(run.RunId, cancelRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("cancelled");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Should().ContainKey("reason");
    }

    [Fact]
    public async Task CancelRunAsync_WithNonExistentRun_ReturnsNull()
    {
        // Arrange
        var cancelRequest = new CancelRunRequest { Reason = "Test cancellation" };

        // Act
        var result = await _store.CancelRunAsync("non-existent-run", cancelRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Run Lifecycle Tests

    [Fact]
    public async Task RunLifecycle_PendingToCompleted_WorksCorrectly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Assert initial state
        run.Status.Should().Be("pending");

        // Act - Complete the run
        var completedRun = await _store.CompleteRunAsync(run.RunId, new CompleteRunRequest
        {
            Timings = new Dictionary<string, object> { ["duration"] = 1000.0 }
        });

        // Assert completed state
        completedRun.Should().NotBeNull();
        completedRun!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task RunLifecycle_PendingToFailed_WorksCorrectly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Assert initial state
        run.Status.Should().Be("pending");

        // Act - Fail the run
        var failedRun = await _store.FailRunAsync(run.RunId, new FailRunRequest
        {
            ErrorMessage = "Processing error"
        });

        // Assert failed state
        failedRun.Should().NotBeNull();
        failedRun!.Status.Should().Be("failed");
    }

    [Fact]
    public async Task RunLifecycle_PendingToCancelled_WorksCorrectly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        // Assert initial state
        run.Status.Should().Be("pending");

        // Act - Cancel the run
        var cancelledRun = await _store.CancelRunAsync(run.RunId, new CancelRunRequest
        {
            Reason = "Timeout"
        });

        // Assert cancelled state
        cancelledRun.Should().NotBeNull();
        cancelledRun!.Status.Should().Be("cancelled");
    }

    #endregion

    #region Complex Data Tests

    [Fact]
    public async Task CompleteRunAsync_WithComplexTimingsAndCosts_SerializesCorrectly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        var completeRequest = new CompleteRunRequest
        {
            Timings = new Dictionary<string, object>
            {
                ["duration"] = 2500.75,
                ["llm_time"] = 2000.0,
                ["connector_time"] = 450.5,
                ["sandbox_time"] = 50.25,
                ["breakdown"] = new Dictionary<string, object>
                {
                    ["init"] = 10.0,
                    ["execution"] = 2400.0,
                    ["cleanup"] = 90.75
                }
            },
            Costs = new Dictionary<string, object>
            {
                ["tokens"] = 5000,
                ["prompt_tokens"] = 3000,
                ["completion_tokens"] = 2000,
                ["usd"] = 0.75,
                ["model"] = "gpt-4"
            }
        };

        // Act
        var result = await _store.CompleteRunAsync(run.RunId, completeRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Timings.Should().NotBeNull();
        result.Timings!.Should().ContainKey("duration");
        result.Costs.Should().NotBeNull();
        result.Costs!.Should().ContainKey("tokens");
    }

    [Fact]
    public async Task FailRunAsync_WithDetailedErrorInfo_SerializesCorrectly()
    {
        // Arrange
        var run = await _store.CreateRunAsync("agent-1", "1.0.0");

        var failRequest = new FailRunRequest
        {
            ErrorMessage = "Service Bus connection failed",
            ErrorDetails = "Error code: SB_CONNECTION_TIMEOUT, Retry count: 3, Queue: invoices",
            Timings = new Dictionary<string, object>
            {
                ["duration"] = 15000.0,
                ["time_to_failure"] = 14500.0
            }
        };

        // Act
        var result = await _store.FailRunAsync(run.RunId, failRequest);

        // Assert
        result.Should().NotBeNull();
        result!.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Should().ContainKey("errorMessage");
        result.ErrorInfo.Should().ContainKey("errorDetails");
    }

    #endregion
}
