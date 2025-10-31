using System.Diagnostics;
using System.Text.Json;
using Node.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace Node.Runtime.Services;

/// <summary>
/// Interface for executing agents in isolated sandbox processes.
/// </summary>
public interface ISandboxExecutor
{
    /// <summary>
    /// Executes an agent in an isolated process with budget enforcement.
    /// </summary>
    /// <param name="spec">Agent specification containing definition and budget.</param>
    /// <param name="input">Input message for the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the agent execution.</returns>
    Task<AgentExecutionResult> ExecuteAsync(
        AgentSpec spec,
        string input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for executing agents in isolated sandbox processes.
/// Provides process isolation, budget enforcement, and secure cleanup.
/// </summary>
public sealed class SandboxExecutorService : ISandboxExecutor, IAgentExecutor
{
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger<SandboxExecutorService> _logger;
    private readonly string _agentHostPath;

    public SandboxExecutorService(
        IOptions<AgentRuntimeOptions> options,
        ILogger<SandboxExecutorService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Determine the path to the Agent.Host executable
        _agentHostPath = FindAgentHostExecutable();

        _logger.LogInformation("SandboxExecutor initialized with Agent.Host at: {Path}", _agentHostPath);
    }

    public async Task<AgentExecutionResult> ExecuteAsync(
        AgentSpec spec,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting sandbox execution for agent {AgentId} v{Version}",
            spec.AgentId, spec.Version);

        Process? process = null;

        try
        {
            // Apply budget constraints
            var maxTokens = spec.Budget?.MaxTokens ?? _options.MaxTokens;
            var maxDurationSeconds = spec.Budget?.MaxDurationSeconds ?? _options.MaxDurationSeconds;
            var timeout = TimeSpan.FromSeconds(maxDurationSeconds);

            // Create the execution request
            var request = new AgentExecutionRequest
            {
                AgentId = spec.AgentId,
                Version = spec.Version,
                Name = spec.Name,
                Instructions = spec.Instructions,
                Input = input,
                MaxTokens = maxTokens,
                MaxDurationSeconds = maxDurationSeconds,
                ModelProfile = spec.ModelProfile
            };

            var requestJson = JsonSerializer.Serialize(request);

            // Configure the process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _agentHostPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = processStartInfo };

            // Set up output capture
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            // Start the process
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Agent.Host process");
            }

            _logger.LogDebug("Sandbox process started with PID {ProcessId}", process.Id);

            // Begin async reading of output and error streams
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Write the request to stdin
            await process.StandardInput.WriteLineAsync(requestJson);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Wait for process to complete with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout.Add(TimeSpan.FromSeconds(5))); // Add 5s buffer for process overhead

            var completed = await WaitForExitAsync(process, timeoutCts.Token);

            stopwatch.Stop();

            if (!completed)
            {
                // Timeout - kill the process
                _logger.LogWarning(
                    "Sandbox process {ProcessId} exceeded timeout ({TimeoutSeconds}s), terminating",
                    process.Id, maxDurationSeconds);

                KillProcessTree(process);

                return new AgentExecutionResult
                {
                    Success = false,
                    Error = $"Agent execution exceeded maximum duration of {maxDurationSeconds} seconds",
                    Duration = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>()
                };
            }

            // Process completed - parse the output
            var output = outputBuilder.ToString().Trim();
            var errorOutput = errorBuilder.ToString().Trim();

            if (!string.IsNullOrEmpty(errorOutput))
            {
                _logger.LogWarning("Sandbox process stderr: {ErrorOutput}", errorOutput);
            }

            if (string.IsNullOrEmpty(output))
            {
                return new AgentExecutionResult
                {
                    Success = false,
                    Error = "No output received from sandbox process",
                    Duration = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["exit_code"] = process.ExitCode,
                        ["stderr"] = errorOutput
                    }
                };
            }

            // Deserialize the response
            var response = JsonSerializer.Deserialize<AgentExecutionResponse>(output);

            if (response == null)
            {
                return new AgentExecutionResult
                {
                    Success = false,
                    Error = "Failed to deserialize sandbox response",
                    Duration = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["raw_output"] = output
                    }
                };
            }

            // Map response to result
            var result = new AgentExecutionResult
            {
                Success = response.Success,
                Output = response.Output,
                Error = response.Error,
                TokensIn = response.TokensIn,
                TokensOut = response.TokensOut,
                Duration = TimeSpan.FromMilliseconds(response.DurationMs),
                UsdCost = response.UsdCost,
                Metadata = new Dictionary<string, object>
                {
                    ["process_id"] = process.Id,
                    ["exit_code"] = process.ExitCode,
                    ["sandboxed"] = true
                }
            };

            _logger.LogInformation(
                "Sandbox execution completed: Success={Success}, Duration={DurationMs}ms, Tokens={TokensIn}/{TokensOut}, Cost=${Cost:F4}",
                result.Success, result.Duration.TotalMilliseconds, result.TokensIn, result.TokensOut, result.UsdCost);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sandbox execution cancelled for agent {AgentId}", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Process execution error for agent {AgentId} in sandbox", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            return new AgentExecutionResult
            {
                Success = false,
                Error = $"Process error: {ex.Message}",
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>()
            };
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Sandbox execution timeout for agent {AgentId}", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            return new AgentExecutionResult
            {
                Success = false,
                Error = "Execution timeout",
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>()
            };
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Invalid operation during sandbox execution for agent {AgentId}", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            return new AgentExecutionResult
            {
                Success = false,
                Error = ex.Message,
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>()
            };
        }
        catch (IOException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "I/O error during sandbox execution for agent {AgentId}", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            return new AgentExecutionResult
            {
                Success = false,
                Error = $"I/O error: {ex.Message}",
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Unexpected error executing agent {AgentId} in sandbox", spec.AgentId);

            if (process != null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            return new AgentExecutionResult
            {
                Success = false,
                Error = $"Sandbox execution error: {ex.Message}",
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>()
            };
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Waits for a process to exit with cancellation support.
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Kills a process and all its child processes.
    /// </summary>
    private void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogDebug("Killed sandbox process {ProcessId} and its children", process.Id);
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Win32 error killing sandbox process {ProcessId}", process.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation killing sandbox process {ProcessId}", process.Id);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Kill not supported for sandbox process {ProcessId}", process.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error killing sandbox process {ProcessId}", process.Id);
        }
    }

    /// <summary>
    /// Finds the Agent.Host executable.
    /// </summary>
    private string FindAgentHostExecutable()
    {
        var currentDir = AppContext.BaseDirectory;

        // Determine the executable name based on OS
        var exeName = OperatingSystem.IsWindows() ? "Agent.Host.exe" : "Agent.Host";

        // Location 1: Same directory as Node.Runtime (after CopyAgentHost target)
        var paths = new[]
        {
            Path.Combine(currentDir, exeName),
            Path.Combine(currentDir, "Agent.Host", exeName),
            Path.Combine(currentDir, "..", "Agent.Host", exeName),
            Path.Combine(currentDir, "..", "..", "..", "..", "Agent.Host", "bin", "Debug", "net9.0", exeName),
            Path.Combine(currentDir, "..", "..", "..", "..", "Agent.Host", "bin", "Release", "net9.0", exeName)
        };

        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Throw if not found
        throw new FileNotFoundException(
            $"Agent.Host executable not found. Searched locations: {string.Join(", ", paths.Select(Path.GetFullPath))}");
    }
}

/// <summary>
/// Request model for Agent.Host IPC.
/// </summary>
internal sealed class AgentExecutionRequest
{
    public required string AgentId { get; set; }
    public required string Version { get; set; }
    public required string Name { get; set; }
    public required string Instructions { get; set; }
    public required string Input { get; set; }
    public int? MaxTokens { get; set; }
    public int? MaxDurationSeconds { get; set; }
    public Dictionary<string, object>? ModelProfile { get; set; }
}

/// <summary>
/// Response model for Agent.Host IPC.
/// </summary>
internal sealed class AgentExecutionResponse
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public long DurationMs { get; set; }
    public double UsdCost { get; set; }
}
