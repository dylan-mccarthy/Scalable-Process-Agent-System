using ControlPlane.Api.Models;
using System.Collections.Concurrent;

namespace ControlPlane.Api.Services;

public interface IRunStore
{
    Task<Run?> GetRunAsync(string runId);
    Task<IEnumerable<Run>> GetAllRunsAsync();
    Task<Run> CreateRunAsync(string agentId, string version);
    Task<Run?> CompleteRunAsync(string runId, CompleteRunRequest request);
    Task<Run?> FailRunAsync(string runId, FailRunRequest request);
    Task<Run?> CancelRunAsync(string runId, CancelRunRequest request);
}

public class InMemoryRunStore : IRunStore
{
    private readonly ConcurrentDictionary<string, Run> _runs = new();

    public Task<Run?> GetRunAsync(string runId)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<IEnumerable<Run>> GetAllRunsAsync()
    {
        return Task.FromResult<IEnumerable<Run>>(_runs.Values.ToList());
    }

    public Task<Run> CreateRunAsync(string agentId, string version)
    {
        var run = new Run
        {
            RunId = Guid.NewGuid().ToString(),
            AgentId = agentId,
            Version = version,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _runs[run.RunId] = run;
        return Task.FromResult(run);
    }

    public Task<Run?> CompleteRunAsync(string runId, CompleteRunRequest request)
    {
        if (!_runs.TryGetValue(runId, out var run))
        {
            return Task.FromResult<Run?>(null);
        }

        run.Status = "completed";
        run.Timings = request.Timings;
        run.Costs = request.Costs;
        _runs[runId] = run;
        return Task.FromResult<Run?>(run);
    }

    public Task<Run?> FailRunAsync(string runId, FailRunRequest request)
    {
        if (!_runs.TryGetValue(runId, out var run))
        {
            return Task.FromResult<Run?>(null);
        }

        run.Status = "failed";
        run.Timings = request.Timings;
        run.ErrorInfo = new Dictionary<string, object>
        {
            ["errorMessage"] = request.ErrorMessage
        };
        if (request.ErrorDetails != null)
        {
            run.ErrorInfo["errorDetails"] = request.ErrorDetails;
        }
        _runs[runId] = run;
        return Task.FromResult<Run?>(run);
    }

    public Task<Run?> CancelRunAsync(string runId, CancelRunRequest request)
    {
        if (!_runs.TryGetValue(runId, out var run))
        {
            return Task.FromResult<Run?>(null);
        }

        run.Status = "cancelled";
        run.ErrorInfo = new Dictionary<string, object>
        {
            ["cancelReason"] = request.Reason
        };
        _runs[runId] = run;
        return Task.FromResult<Run?>(run);
    }
}
