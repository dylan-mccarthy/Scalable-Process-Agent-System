# Azure AI Foundry Tool Registry

This document describes the Azure AI Foundry tool provider integration in the Business Process Agents MVP platform.

## Overview

The Azure AI Foundry Tool Provider (`AzureAIFoundryToolProvider`) registers Azure AI Foundry's built-in tools with the Microsoft Agent Framework (MAF) SDK tool registry. This integration enables agents to leverage Azure AI Foundry's powerful capabilities for code execution, file search, web grounding, and more.

## Architecture

### Components

1. **`IAzureAIFoundryToolProvider`** - Interface for the Azure AI Foundry tool provider
2. **`AzureAIFoundryToolProvider`** - Implementation that registers Azure AI Foundry tools
3. **`IToolRegistry`** - Existing tool registry interface (implemented by `InMemoryToolRegistry`)

### Integration Flow

```
Application Startup
    ↓
DI Container Initialization
    ↓
AzureAIFoundryToolProvider Service Registration
    ↓
Application Build
    ↓
RegisterAzureAIFoundryToolsAsync() Called
    ↓
8 Built-in Tools Registered with ToolRegistry
    ↓
Tools Available for Agent Association
```

## Registered Tools

The provider registers 8 Azure AI Foundry built-in tools:

### 1. Code Interpreter
- **Name**: `CodeInterpreter`
- **Category**: Execution
- **Description**: Enables agents to write and run Python code in a sandboxed execution environment
- **Capabilities**: 
  - Python code execution
  - Data analysis
  - File generation
  - Visualization creation
- **Sandboxed**: Yes

### 2. File Search
- **Name**: `FileSearch`
- **Category**: Retrieval
- **Description**: Augments agents with knowledge from proprietary documents using RAG
- **Features**:
  - Automatic chunking (800 tokens, 400 overlap)
  - Semantic search with text-embedding-3-large
  - Supports up to 10,000 files per vector store
  - Maximum file size: 512MB
  - Maximum chunks in context: 20

### 3. Azure AI Search
- **Name**: `AzureAISearch`
- **Category**: Retrieval
- **Description**: Enterprise-grade search with vector, full-text, and hybrid search capabilities
- **Capabilities**:
  - Vector search
  - Full-text search
  - Hybrid search
- **Requirements**: Existing Azure AI Search index

### 4. Bing Grounding
- **Name**: `BingGrounding`
- **Category**: Retrieval
- **Description**: Real-time web search integration for up-to-date information
- **Features**:
  - Web search
  - Real-time data
  - Grounded responses

### 5. Function Calling
- **Name**: `FunctionCalling`
- **Category**: Integration
- **Description**: Enables agents to call custom functions with defined schemas
- **Features**:
  - Custom business logic integration
  - Schema-based function definitions
  - Automatic orchestration

### 6. Azure Functions
- **Name**: `AzureFunctions`
- **Category**: Execution
- **Description**: Serverless code execution for event-driven automation
- **Execution Modes**:
  - Synchronous
  - Asynchronous
  - Long-running
  - Event-driven

### 7. OpenAPI
- **Name**: `OpenAPI`
- **Category**: Integration
- **Description**: Connect agents to external REST APIs using OpenAPI 3.0 specifications
- **Features**:
  - OpenAPI 3.0 spec support
  - Authentication: None, API Key, OAuth2
  - Automatic API client generation

### 8. Browser Automation
- **Name**: `BrowserAutomation`
- **Category**: Automation
- **Description**: Perform real-world browser tasks through natural language prompts
- **Capabilities**:
  - Web navigation
  - UI interaction
  - Data extraction
- **Mode**: Headless

## Usage

### Associating Tools with Agents

Tools can be associated with specific agents using the tool registry:

```csharp
// Get the tool registry service
var toolRegistry = app.Services.GetRequiredService<IToolRegistry>();

// Associate tools with an agent
await toolRegistry.AssociateToolWithAgentAsync("invoice-classifier", "CodeInterpreter");
await toolRegistry.AssociateToolWithAgentAsync("invoice-classifier", "FunctionCalling");

// Retrieve tools for an agent
var agentTools = await toolRegistry.GetToolsForAgentAsync("invoice-classifier");
```

### Getting Available Tool Types

```csharp
var toolProvider = app.Services.GetRequiredService<IAzureAIFoundryToolProvider>();
var availableTools = toolProvider.GetAvailableToolTypes();
// Returns: ["CodeInterpreter", "FileSearch", "AzureAISearch", "BingGrounding", 
//           "FunctionCalling", "AzureFunctions", "OpenAPI", "BrowserAutomation"]
```

## Configuration

### Service Registration

The Azure AI Foundry tool provider is registered in `Program.cs`:

```csharp
// Register tool registry and Azure AI Foundry provider
builder.Services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
builder.Services.AddSingleton<IAzureAIFoundryToolProvider, AzureAIFoundryToolProvider>();
```

### Initialization

Tools are automatically registered on application startup:

```csharp
// Initialize Azure AI Foundry tools with the tool registry
try
{
    var azureAIFoundryProvider = app.Services.GetRequiredService<IAzureAIFoundryToolProvider>();
    await azureAIFoundryProvider.RegisterAzureAIFoundryToolsAsync();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Azure AI Foundry tools registered successfully");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to register Azure AI Foundry tools");
}
```

## Tool Definition Schema

Each tool is registered with the following structure:

```csharp
{
    "Name": "ToolName",
    "Description": "Tool description",
    "Type": "azure-ai-foundry",
    "Configuration": {
        "provider": "Azure AI Foundry",
        "category": "execution|retrieval|integration|automation",
        // Tool-specific configuration properties
    }
}
```

## Integration with Microsoft Agent Framework

The tool registry integrates with MAF SDK's agent creation process. When creating agents, registered tools can be specified:

```csharp
// Create agent with Azure AI Foundry tools (future implementation)
var chatClient = GetChatClient(modelProfile);
var aiAgent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant",
    name: "InvoiceClassifier",
    tools: [/* MAF tool definitions based on registered tools */]
);
```

## Extensibility

### Adding Custom Tools

The tool registry supports registering custom tools alongside Azure AI Foundry tools:

```csharp
var customTool = new ToolDefinition
{
    Name = "CustomInvoiceParser",
    Description = "Parses invoice documents using custom logic",
    Type = "custom",
    Configuration = new Dictionary<string, object>
    {
        { "provider", "Custom" },
        { "endpoint", "https://api.example.com/parse" }
    }
};

await toolRegistry.RegisterToolAsync(customTool);
```

## Monitoring and Logging

The tool provider includes comprehensive logging:

- **Info**: Tool registration success
- **Debug**: Individual tool registration
- **Error**: Registration failures

Example log output:

```
[Information] Registering Azure AI Foundry built-in tools with tool registry
[Debug] Registered Code Interpreter tool
[Debug] Registered File Search tool
...
[Information] Successfully registered 8 Azure AI Foundry tools
```

## Testing

Comprehensive unit tests verify:

- All 8 tools are registered
- Each tool has correct configuration
- Tools can be associated with agents
- Multiple registration calls are idempotent
- Proper error handling

Test suite: `AzureAIFoundryToolProviderTests.cs` (16 tests)

## References

- [Azure AI Foundry Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [Azure AI Foundry Agent Service Tools](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/overview)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [System Architecture Document](../sad.md)
- [Tasks Overview](../tasks.yaml)

## Related Tasks

- **E3-T4**: Azure AI Foundry integration (LLM endpoint configuration)
- **E3-T5**: Tool registry setup (this implementation)
- **E3-T6**: Invoice Classifier agent (uses registered tools)
