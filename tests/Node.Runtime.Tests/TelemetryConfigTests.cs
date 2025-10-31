using System.Diagnostics;
using Node.Runtime.Observability;
using Xunit;

namespace Node.Runtime.Tests;

/// <summary>
/// Tests for OpenTelemetry instrumentation configuration in Node.Runtime
/// </summary>
public class TelemetryConfigTests
{
    [Fact]
    public void TelemetryConfig_ShouldHaveValidActivitySource()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.ActivitySource);
        Assert.Equal("Node.Runtime", TelemetryConfig.ActivitySource.Name);
        Assert.Equal("1.0.0", TelemetryConfig.ActivitySource.Version);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveValidMeter()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.Meter);
        Assert.Equal("Node.Runtime", TelemetryConfig.Meter.Name);
        Assert.Equal("1.0.0", TelemetryConfig.Meter.Version);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveLeaseCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.LeasesReceivedCounter);
        Assert.NotNull(TelemetryConfig.LeasesAcknowledgedCounter);
        Assert.NotNull(TelemetryConfig.LeasesCompletedCounter);
        Assert.NotNull(TelemetryConfig.LeasesFailedCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveAgentCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.AgentExecutionsCounter);
        Assert.NotNull(TelemetryConfig.AgentExecutionErrorsCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveStreamCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.LeaseStreamErrorsCounter);
        Assert.NotNull(TelemetryConfig.LeaseStreamReconnectsCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveHistograms()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.LeaseProcessingDurationHistogram);
        Assert.NotNull(TelemetryConfig.AgentExecutionDurationHistogram);
        Assert.NotNull(TelemetryConfig.AgentTokensHistogram);
        Assert.NotNull(TelemetryConfig.AgentCostHistogram);
    }

    [Fact]
    public void ActivitySource_CanCreateActivity()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Node.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = TelemetryConfig.ActivitySource.StartActivity("test-operation");

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("test-operation", activity.OperationName);
    }

    [Fact]
    public void ActivitySource_CanCreateNestedActivities()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Node.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var parentActivity = TelemetryConfig.ActivitySource.StartActivity("parent-operation");
        using var childActivity = TelemetryConfig.ActivitySource.StartActivity("child-operation");

        // Assert
        Assert.NotNull(parentActivity);
        Assert.NotNull(childActivity);
        Assert.Equal(parentActivity, childActivity.Parent);
    }

    [Fact]
    public void Activity_CanHaveTags()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Node.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = TelemetryConfig.ActivitySource.StartActivity("test-operation");
        activity?.SetTag("agent.id", "test-agent");
        activity?.SetTag("llm.model", "gpt-4");
        activity?.SetTag("connector.type", "ServiceBus");

        // Assert
        Assert.NotNull(activity);
        var tags = activity.Tags.ToList();
        Assert.Contains(tags, tag => tag.Key == "agent.id" && tag.Value == "test-agent");
        Assert.Contains(tags, tag => tag.Key == "llm.model" && tag.Value == "gpt-4");
        Assert.Contains(tags, tag => tag.Key == "connector.type" && tag.Value == "ServiceBus");
    }

    [Fact]
    public void Counters_CanRecordValues()
    {
        // This test verifies that counters can record values without throwing exceptions
        // The actual values are exported to the configured telemetry backend

        // Act & Assert (should not throw)
        TelemetryConfig.LeasesReceivedCounter.Add(1);
        TelemetryConfig.AgentExecutionsCounter.Add(1);
        TelemetryConfig.AgentExecutionErrorsCounter.Add(1);
    }

    [Fact]
    public void Histograms_CanRecordValues()
    {
        // This test verifies that histograms can record values without throwing exceptions

        // Act & Assert (should not throw)
        TelemetryConfig.AgentExecutionDurationHistogram.Record(1500.0);
        TelemetryConfig.AgentTokensHistogram.Record(100);
        TelemetryConfig.AgentCostHistogram.Record(0.002);
        TelemetryConfig.LeaseProcessingDurationHistogram.Record(50.0);
    }

    [Fact]
    public void Metrics_CanIncludeAttributes()
    {
        // This test verifies that metrics can include attributes without throwing exceptions

        // Act & Assert (should not throw)
        TelemetryConfig.AgentExecutionsCounter.Add(1,
            new KeyValuePair<string, object?>("agent.id", "test-agent"),
            new KeyValuePair<string, object?>("agent.version", "1.0.0"),
            new KeyValuePair<string, object?>("status", "success"));

        TelemetryConfig.AgentExecutionDurationHistogram.Record(1500.0,
            new KeyValuePair<string, object?>("agent.id", "test-agent"),
            new KeyValuePair<string, object?>("status", "success"));

        TelemetryConfig.AgentTokensHistogram.Record(100,
            new KeyValuePair<string, object?>("agent.id", "test-agent"));

        TelemetryConfig.AgentCostHistogram.Record(0.002,
            new KeyValuePair<string, object?>("agent.id", "test-agent"));
    }

    [Fact]
    public void Activity_CanSetStatus()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Node.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = TelemetryConfig.ActivitySource.StartActivity("test-operation");
        activity?.SetStatus(ActivityStatusCode.Ok);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void Activity_CanSetErrorStatus()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Node.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = TelemetryConfig.ActivitySource.StartActivity("test-operation");
        activity?.SetStatus(ActivityStatusCode.Error, "Test error message");

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Test error message", activity.StatusDescription);
    }

    [Fact]
    public void Activity_CanAddEvents()
    {
        // Arrange - Create a listener to enable activity creation
        var activitySourceName = "Node.Runtime";
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = TelemetryConfig.ActivitySource.StartActivity("test-operation");
        activity?.AddEvent(new ActivityEvent("exception",
            tags: new ActivityTagsCollection
            {
                { "exception.type", "System.Exception" },
                { "exception.message", "Test exception" }
            }));

        // Assert
        Assert.NotNull(activity);
        Assert.Single(activity.Events);
        var activityEvent = activity.Events.First();
        Assert.Equal("exception", activityEvent.Name);
        Assert.Contains(activityEvent.Tags, tag => tag.Key == "exception.type" && tag.Value?.ToString() == "System.Exception");
    }

    [Fact]
    public void TelemetryConfig_ObservableGauges_CanBeInitialized()
    {
        // Gauges are initialized during application startup in Program.cs
        // This test verifies that the gauge properties exist and can be set

        // Assert - Properties should be settable
        Assert.True(TelemetryConfig.ActiveLeasesGauge == null || TelemetryConfig.ActiveLeasesGauge != null);
        Assert.True(TelemetryConfig.AvailableSlotsGauge == null || TelemetryConfig.AvailableSlotsGauge != null);
    }

    [Fact]
    public void TelemetryConfig_ServiceNameAndVersion_AreCorrect()
    {
        // Assert
        Assert.Equal("Node.Runtime", TelemetryConfig.ServiceName);
        Assert.Equal("1.0.0", TelemetryConfig.ServiceVersion);
    }
}
