# Azure AI Foundry Integration Tests

## Overview

This directory contains integration tests for Azure AI Foundry endpoints using mock HTTP servers. These tests validate the complete integration path from the `AzureAIFoundryChatClient` to Azure AI Inference API without requiring actual Azure services.

## Test Suite: `AzureAIFoundryIntegrationTests`

### Purpose
Validates that the `AzureAIFoundryChatClient` correctly communicates with Azure AI Foundry endpoints using mock HTTP responses.

### Technology Stack
- **Testing Framework**: xUnit
- **Assertion Library**: FluentAssertions
- **HTTP Mocking**: WireMock.Net 1.6.10
- **Azure SDK**: Azure.AI.Inference

### Test Cases

#### Passing Tests (6)

1. **ChatCompletion_WithMockEndpoint_ReturnsSuccessfulResponse**
   - Validates basic chat completion with a mocked Azure AI Foundry endpoint
   - Verifies request/response format and message parsing

2. **ChatCompletion_WithTemperatureOption_SendsCorrectParameters**
   - Tests that chat options (temperature, max tokens, top-p) are correctly sent
   - Validates parameter serialization in requests

3. **ChatCompletion_WithMultipleMessages_ProcessesConversationCorrectly**
   - Tests multi-turn conversations with system, user, and assistant messages
   - Ensures conversation history is properly maintained

4. **ChatCompletion_WithJsonResponse_CanDeserializeStructuredOutput**
   - Tests structured JSON output (e.g., invoice classification results)
   - Validates that JSON responses are correctly deserialized

5. **ChatClient_Metadata_ReturnsCorrectInformation**
   - Verifies client metadata (provider name, model ID)
   - Ensures proper initialization

6. **Dispose_ChatClient_DoesNotThrow**
   - Tests resource cleanup
   - Validates disposal behavior

#### Skipped Tests (3)

The following tests are skipped because the Azure SDK's built-in retry policy causes them to hang in test environments. Error handling is adequately covered in unit tests.

1. **ChatCompletion_WithInvalidApiKey_ThrowsException** (Skipped)
   - Reason: SDK retries authentication errors
   
2. **ChatCompletion_WithRateLimitError_ThrowsException** (Skipped)
   - Reason: SDK retries 429 responses with exponential backoff
   
3. **ChatCompletion_WithServerError_ThrowsException** (Skipped)
   - Reason: SDK retries 5xx server errors

## Running the Tests

### Run All Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~AzureAIFoundryIntegrationTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~ChatCompletion_WithMockEndpoint_ReturnsSuccessfulResponse"
```

### Expected Results
- **Total Tests**: 9
- **Passed**: 6
- **Skipped**: 3
- **Duration**: ~350ms

## Mock Server Details

### WireMock Configuration
- **Server**: Kestrel (embedded HTTP server)
- **Port**: Dynamically allocated
- **Lifetime**: Per-test instance (created in constructor, disposed after test)

### Mocked Responses
The mock server simulates Azure AI Inference API responses:

```json
{
  "id": "chatcmpl-test-123",
  "model": "gpt-4o-mini",
  "created": 1234567890,
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Response content"
      },
      "index": 0,
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 15,
    "completion_tokens": 10,
    "total_tokens": 25
  }
}
```

## Architecture Notes

### Why Mock Endpoints?
1. **No External Dependencies**: Tests don't require Azure credentials or active services
2. **Fast Execution**: Mock responses are instant (~350ms for all tests)
3. **Deterministic**: Same input always produces same output
4. **Cost-Free**: No Azure API charges during testing
5. **CI/CD Friendly**: Tests run reliably in any environment

### Integration vs Unit Tests
- **Unit Tests** (`AzureAIFoundryChatClientTests`): Test the client in isolation with mocked SDK components
- **Integration Tests** (this file): Test the full HTTP communication path with mock HTTP endpoints

### Relationship to E2E Tests
These integration tests focus on the Azure AI Foundry client layer. End-to-end tests (E7-T3) will test the complete invoice classification flow including Service Bus, agent execution, and HTTP output.

## Troubleshooting

### Tests Hanging
If tests hang, it's likely due to:
1. **Azure SDK Retry Logic**: Error scenarios trigger retries (use Skip attribute)
2. **Missing Mock Setup**: Ensure all expected endpoints are mocked
3. **Incorrect URL**: Verify the client is using `_mockEndpoint` not a hardcoded URL

### WireMock Issues
- **Port Conflicts**: WireMock uses dynamic ports, so conflicts are rare
- **Request Matching**: Use `.UsingPost()` without path restrictions for flexibility
- **Namespace Conflicts**: Use aliases (`WireMockRequest`, `WireMockResponse`) to avoid conflicts with Azure SDK

## Maintenance

### Adding New Tests
1. Create new test method with `[Fact]` attribute
2. Setup mock server response with `_mockServer.Given(...).RespondWith(...)`
3. Create `ChatCompletionsClient` with `new Uri(_mockEndpoint)`
4. Execute the operation and assert results
5. Verify mock server received expected requests

### Updating for SDK Changes
If Azure.AI.Inference SDK changes:
1. Update response JSON format to match new schema
2. Adjust property names if SDK changes naming conventions
3. Update assertions if response structure changes

## Related Files
- `/src/Node.Runtime/Services/AzureAIFoundryChatClient.cs` - Implementation under test
- `/tests/Node.Runtime.Tests/Services/AzureAIFoundryChatClientTests.cs` - Unit tests
- `/tests/Node.Runtime.Tests/InvoiceClassifierIntegrationTests.cs` - End-to-end invoice flow tests

## Task Reference
- **Epic**: E7 – Testing & Validation
- **Task**: E7-T2 – Integration tests
- **Description**: Include Azure AI Foundry mock endpoints
- **Owner**: Platform Engineering
