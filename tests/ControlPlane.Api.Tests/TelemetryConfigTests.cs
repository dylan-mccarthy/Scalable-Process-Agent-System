using System.Diagnostics;
using System.Diagnostics.Metrics;
using ControlPlane.Api.Observability;
using Xunit;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Tests for OpenTelemetry instrumentation configuration
/// </summary>
public class TelemetryConfigTests
{
    [Fact]
    public void TelemetryConfig_ShouldHaveValidActivitySource()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.ActivitySource);
        Assert.Equal("ControlPlane.Api", TelemetryConfig.ActivitySource.Name);
        Assert.Equal("1.0.0", TelemetryConfig.ActivitySource.Version);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveValidMeter()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.Meter);
        Assert.Equal("ControlPlane.Api", TelemetryConfig.Meter.Name);
        Assert.Equal("1.0.0", TelemetryConfig.Meter.Version);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveRunCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.RunsStartedCounter);
        Assert.NotNull(TelemetryConfig.RunsCompletedCounter);
        Assert.NotNull(TelemetryConfig.RunsFailedCounter);
        Assert.NotNull(TelemetryConfig.RunsCancelledCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveNodeCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.NodesRegisteredCounter);
        Assert.NotNull(TelemetryConfig.NodesDisconnectedCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveLeaseCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.LeasesGrantedCounter);
        Assert.NotNull(TelemetryConfig.LeasesReleasedCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveSchedulingCounters()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.SchedulingAttemptsCounter);
        Assert.NotNull(TelemetryConfig.SchedulingFailuresCounter);
    }

    [Fact]
    public void TelemetryConfig_ShouldHaveHistograms()
    {
        // Assert
        Assert.NotNull(TelemetryConfig.RunDurationHistogram);
        Assert.NotNull(TelemetryConfig.SchedulingDurationHistogram);
        Assert.NotNull(TelemetryConfig.RunTokensHistogram);
        Assert.NotNull(TelemetryConfig.RunCostHistogram);
    }

    [Fact]
    public void ActivitySource_CanCreateActivity()
    {
        // Arrange - Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryConfig.ActivitySource.Name,
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
    public void Counters_CanRecordValues()
    {
        // This test verifies that counters can record values without throwing exceptions
        // The actual values are exported to the configured telemetry backend
        
        // Act & Assert (should not throw)
        TelemetryConfig.RunsStartedCounter.Add(1);
        TelemetryConfig.RunsCompletedCounter.Add(1);
        TelemetryConfig.NodesRegisteredCounter.Add(1);
    }

    [Fact]
    public void Histograms_CanRecordValues()
    {
        // This test verifies that histograms can record values without throwing exceptions
        
        // Act & Assert (should not throw)
        TelemetryConfig.RunDurationHistogram.Record(1500.0);
        TelemetryConfig.SchedulingDurationHistogram.Record(50.0);
        TelemetryConfig.RunTokensHistogram.Record(100);
        TelemetryConfig.RunCostHistogram.Record(0.002);
    }

    [Fact]
    public void Metrics_CanIncludeAttributes()
    {
        // This test verifies that metrics can include attributes without throwing exceptions
        
        // Act & Assert (should not throw)
        TelemetryConfig.RunsStartedCounter.Add(1, 
            new KeyValuePair<string, object?>("agent.id", "test-agent"),
            new KeyValuePair<string, object?>("agent.version", "1.0.0"));
        
        TelemetryConfig.RunDurationHistogram.Record(1500.0,
            new KeyValuePair<string, object?>("agent.id", "test-agent"),
            new KeyValuePair<string, object?>("status", "completed"));
    }

    [Fact]
    public void TelemetryConfig_ObservableGaugeProperties_ShouldBeNullableAndSettable()
    {
        // This test verifies that observable gauge properties can be set
        // The actual gauges are initialized in Program.cs when the app starts
        
        // Act - Set a test gauge
        var testGauge = TelemetryConfig.Meter.CreateObservableGauge(
            "test_gauge",
            () => 42,
            description: "Test gauge");
        
        TelemetryConfig.ActiveRunsGauge = testGauge;
        
        // Assert - Should be settable
        Assert.NotNull(TelemetryConfig.ActiveRunsGauge);
        
        // Clean up
        TelemetryConfig.ActiveRunsGauge = null;
    }
}
