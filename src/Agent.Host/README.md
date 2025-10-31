# Agent.Host

The Agent.Host is an isolated process executable that runs business process agents in a sandboxed environment. It is spawned by the Node Runtime's SandboxExecutorService to provide process-level isolation and security for agent execution.

## Overview

Agent.Host provides:

- **Process Isolation**: Each agent execution runs in its own process, isolated from the Node Runtime
- **Budget Enforcement**: Enforces token and time budgets within the sandboxed process
- **IPC Communication**: Communicates with parent process via stdin/stdout using JSON
- **Graceful Termination**: Can be terminated by parent process without affecting Node Runtime
- **Security**: Runs agent code in isolated process space with limited access

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Node.Runtime                          │
│                                                          │
│  ┌────────────────────────────────────────────────┐    │
│  │       SandboxExecutorService                    │    │
│  │                                                 │    │
│  │  1. Create AgentExecutionRequest (JSON)        │    │
│  │  2. Spawn Agent.Host process                   │    │
│  │  3. Write request to stdin                     │    │
│  │  4. Read response from stdout                  │    │
│  │  5. Terminate if timeout                       │    │
│  └────────────────────────────────────────────────┘    │
│                        │                                │
└────────────────────────┼────────────────────────────────┘
                         │ Process spawn
                         ▼
┌─────────────────────────────────────────────────────────┐
│                  Agent.Host Process                      │
│                                                          │
│  ┌────────────────────────────────────────────────┐    │
│  │              Program.cs                         │    │
│  │                                                 │    │
│  │  1. Read request from stdin                    │    │
│  │  2. Deserialize JSON                           │    │
│  │  3. Create MAF agent with budget               │    │
│  │  4. Execute agent with timeout                 │    │
│  │  5. Capture result                             │    │
│  │  6. Write response to stdout                   │    │
│  └────────────────────────────────────────────────┘    │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## IPC Protocol

### Request (stdin)

The parent process writes a JSON-serialized `AgentExecutionRequest` to stdin:

```json
{
  "AgentId": "invoice-classifier",
  "Version": "1.0.0",
  "Name": "Invoice Classifier",
  "Instructions": "Classify invoices by vendor and route appropriately",
  "Input": "Invoice: Vendor ABC, Amount $1000",
  "MaxTokens": 4000,
  "MaxDurationSeconds": 60,
  "ModelProfile": null
}
```

### Response (stdout)

Agent.Host writes a JSON-serialized `AgentExecutionResponse` to stdout:

```json
{
  "Success": true,
  "Output": "Vendor: ABC, Category: Office Supplies, Route: Accounting",
  "Error": null,
  "TokensIn": 25,
  "TokensOut": 15,
  "DurationMs": 1234,
  "UsdCost": 0.0012
}
```

### Error Response

If execution fails, the response indicates failure:

```json
{
  "Success": false,
  "Output": null,
  "Error": "Agent execution exceeded maximum duration of 60 seconds",
  "TokensIn": 0,
  "TokensOut": 0,
  "DurationMs": 60000,
  "UsdCost": 0.0
}
```

## Exit Codes

- **0**: Successful execution (check `Success` field in response)
- **1**: Execution failed or error occurred

## Budget Enforcement

Agent.Host enforces budgets at the process level:

- **Time Budget**: Uses `CancellationTokenSource` with timeout from `MaxDurationSeconds`
- **Token Budget**: Passed to MAF SDK for enforcement (future implementation with actual LLM)
- **Cost Tracking**: Estimates cost based on token usage

When a budget is exceeded:
1. Cancellation token triggers `OperationCanceledException`
2. Execution stops gracefully
3. Error response written to stdout
4. Process exits with code 1

## Dependencies

- **Microsoft.Agents.AI**: Microsoft Agent Framework SDK
- **System.Text.Json**: JSON serialization for IPC

## Building

Agent.Host is built automatically as part of the Node.Runtime build:

```bash
dotnet build src/Node.Runtime
```

The executable is copied to the Node.Runtime output directory for deployment.

## Running Manually (for testing)

```bash
# Build the executable
dotnet build src/Agent.Host

# Prepare a request
echo '{"AgentId":"test","Version":"1.0.0","Name":"Test","Instructions":"Test","Input":"Hello"}' | dotnet run --project src/Agent.Host
```

## Security Considerations

- **Process Isolation**: Each execution runs in a separate process, limiting the blast radius of failures
- **Resource Limits**: Enforced by parent process via timeout and process termination
- **No Network Access** (future): Process can be run in a restricted environment
- **Sandboxing** (future): Can be enhanced with OS-level sandboxing (containers, seccomp, etc.)

## Microsoft Agent Framework Integration

Agent.Host uses the Microsoft Agent Framework (MAF) SDK to execute agents:

- Creates `IChatClient` from model profile
- Builds `AIAgent` with instructions
- Executes agent with cancellation support
- Captures token usage and cost

**Note**: Actual LLM execution requires Azure AI Foundry or OpenAI credentials (E3-T4). Until configured, execution returns a helpful error message.

## Future Enhancements

- **Streaming Output**: Support streaming responses for long-running agents
- **Resource Limits**: CPU/memory limits via cgroups or job objects
- **Checkpoint/Restore**: Save agent state for long-running executions
- **Enhanced Sandboxing**: Container-based isolation, network restrictions
- **Tool Execution**: Secure execution of agent tools with permission model

## Related Components

- **SandboxExecutorService**: Parent service in Node.Runtime that spawns Agent.Host
- **AgentExecutorService**: In-process executor (legacy, used for reference)
- **LeasePullService**: Orchestrates agent execution via sandbox executor

## License

See the main repository README for license information.
