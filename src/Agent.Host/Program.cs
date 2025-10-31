using System.Diagnostics;
using System.Text.Json;
using Agent.Host.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Agent.Host: Isolated process for executing agents with budget enforcement
// Communication via stdin (request JSON) and stdout (response JSON)

try
{
    // Read request from stdin
    var requestJson = await Console.In.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(requestJson))
    {
        await WriteErrorResponse("No input provided");
        return 1;
    }

    var request = JsonSerializer.Deserialize<AgentExecutionRequest>(requestJson);

    if (request == null)
    {
        await WriteErrorResponse("Failed to deserialize request");
        return 1;
    }

    // Execute the agent
    var response = await ExecuteAgentAsync(request);

    // Write response to stdout
    var responseJson = JsonSerializer.Serialize(response);
    await Console.Out.WriteLineAsync(responseJson);

    return response.Success ? 0 : 1;
}
catch (Exception ex)
{
    await WriteErrorResponse($"Unhandled exception: {ex.Message}");
    return 1;
}

static async Task<AgentExecutionResponse> ExecuteAgentAsync(AgentExecutionRequest request)
{
    var stopwatch = Stopwatch.StartNew();
    var response = new AgentExecutionResponse();

    try
    {
        // Apply budget constraints
        var maxTokens = request.MaxTokens ?? 4000;
        var maxDurationSeconds = request.MaxDurationSeconds ?? 60;
        var timeout = TimeSpan.FromSeconds(maxDurationSeconds);

        // Create timeout cancellation token
        using var timeoutCts = new CancellationTokenSource(timeout);

        // Get chat client from model profile
        // For now, this will throw NotImplementedException until E3-T4 is complete
        var chatClient = GetChatClient(request.ModelProfile);

        // Create AI agent with instructions
        var aiAgent = chatClient.CreateAIAgent(
            instructions: request.Instructions,
            name: request.Name
        );

        // Execute the agent with timeout
        var result = await aiAgent.RunAsync(
            request.Input,
            cancellationToken: timeoutCts.Token);

        stopwatch.Stop();

        response.Success = true;
        response.Output = result.Text;
        response.DurationMs = stopwatch.ElapsedMilliseconds;

        // Extract token usage from response metadata if available
        // For now, we'll use estimated values
        response.TokensIn = EstimateTokens(request.Input);
        response.TokensOut = EstimateTokens(result.Text ?? string.Empty);
        response.UsdCost = EstimateCost(response.TokensIn, response.TokensOut);
    }
    catch (OperationCanceledException)
    {
        // Timeout occurred
        stopwatch.Stop();
        response.Success = false;
        response.Error = $"Agent execution exceeded maximum duration of {request.MaxDurationSeconds ?? 60} seconds";
        response.DurationMs = stopwatch.ElapsedMilliseconds;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        response.Success = false;
        response.Error = ex.Message;
        response.DurationMs = stopwatch.ElapsedMilliseconds;
    }

    return response;
}

static IChatClient GetChatClient(Dictionary<string, object>? modelProfile)
{
    // This will be implemented in E3-T4 (Azure AI Foundry integration)
    // For now, we throw to indicate this needs configuration
    throw new NotImplementedException(
        "Chat client creation needs to be configured with Azure AI Foundry or OpenAI credentials. " +
        "This will be implemented in E3-T4 (Azure AI Foundry integration). " +
        "The agent executor is ready to execute agents once a model provider is configured.");
}

static int EstimateTokens(string text)
{
    if (string.IsNullOrEmpty(text))
        return 0;

    // Rough approximation: 1 token ≈ 4 characters
    return (int)Math.Ceiling(text.Length / 4.0);
}

static double EstimateCost(int tokensIn, int tokensOut)
{
    // Approximate GPT-4 pricing (as of late 2023):
    // $0.03 per 1K input tokens, $0.06 per 1K output tokens
    const double inputCostPer1k = 0.03;
    const double outputCostPer1k = 0.06;

    var inputCost = (tokensIn / 1000.0) * inputCostPer1k;
    var outputCost = (tokensOut / 1000.0) * outputCostPer1k;

    return inputCost + outputCost;
}

static async Task WriteErrorResponse(string errorMessage)
{
    var errorResponse = new AgentExecutionResponse
    {
        Success = false,
        Error = errorMessage,
        DurationMs = 0
    };

    var responseJson = JsonSerializer.Serialize(errorResponse);
    await Console.Out.WriteLineAsync(responseJson);
}
