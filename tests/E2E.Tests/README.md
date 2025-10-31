# E2E Tests

This project contains End-to-End (E2E) tests for the Business Process Agents system, specifically focusing on invoice processing (Task E7-T3).

## Overview

The E2E tests validate the complete invoice processing flow by:
- Processing 100 synthetic invoices through the system
- Validating output accuracy and classification correctness
- Ensuring performance meets acceptance criteria from the System Architecture Document (SAD)

## Acceptance Criteria (SAD Section 10)

The tests validate the following requirements:
- ✅ **Success Rate**: ≥95% of invoices processed successfully
- ✅ **Performance**: p95 latency < 2 seconds
- ✅ **Data Integrity**: Correlation IDs preserved through the flow
- ✅ **Idempotency**: Unique idempotency keys included in all requests

## Running the Tests

### Prerequisites
- .NET 9.0 SDK installed
- Docker (if using Testcontainers for integration tests)

### Run All E2E Tests

From the repository root:

```bash
dotnet test tests/E2E.Tests/E2E.Tests.csproj
```

### Run with Detailed Output

```bash
dotnet test tests/E2E.Tests/E2E.Tests.csproj -v normal
```

### Run Specific Test

```bash
dotnet test tests/E2E.Tests/E2E.Tests.csproj --filter "FullyQualifiedName~Process100Invoices_ValidatesOutputAccuracy"
```

## Test Cases

### 1. Process100Invoices_ValidatesOutputAccuracy
**Main E2E Test**

Processes 100 synthetic invoices and validates:
- All invoices are processed
- Success rate ≥95%
- p95 latency < 2s
- Output accuracy (vendor classification, routing destination)

**Expected Results:**
```
E2E Test Results:
  Total Invoices: 100
  Successful: 100
  Success Rate: 100.00%
  Average Latency: 0.030s
  Min Latency: 0.010s
  Max Latency: 0.049s
  p95 Latency: 0.047s
  Total Duration: ~3s
```

### 2. Process100Invoices_PreservesCorrelationIds
Tests that correlation IDs are preserved throughout the processing flow (10 invoices).

### 3. Process100Invoices_IncludesIdempotencyKeys
Validates that idempotency keys are included in all HTTP requests and are unique (10 invoices).

## Test Data

The tests use synthetic invoice data with the following vendor categories:
- Office Supplies → Procurement Department
- Technology/Hardware → IT Department
- Professional Services → Finance Department
- Utilities → Facilities Management
- Travel & Expenses → HR Department
- Other → General Accounts Payable

## Architecture

The E2E tests simulate the complete invoice processing flow:

```
Synthetic Invoices → Classification (Simulated LLM) → HTTP Output Connector → Mock API
```

## CI/CD Integration

These tests are designed to run in CI/CD pipelines. They use mocked dependencies (HTTP client, message handlers) to ensure:
- Fast execution (< 5 seconds)
- No external dependencies required
- Deterministic results with fixed random seed

## Troubleshooting

### Test Failures

If tests fail due to performance:
1. Check system resources (CPU, memory)
2. Review p95 latency metrics in test output
3. Adjust timeout values if necessary

### Build Issues

If the project doesn't build:
```bash
dotnet restore tests/E2E.Tests/E2E.Tests.csproj
dotnet build tests/E2E.Tests/E2E.Tests.csproj
```

## Future Enhancements

Potential improvements for E2E testing:
- [ ] Integration with real Azure Service Bus (using Testcontainers or dev environment)
- [ ] Integration with actual LLM endpoints (using Azure AI Foundry test endpoints)
- [ ] Chaos testing scenarios (node failures, network issues)
- [ ] Load testing with higher invoice volumes (1000+)
- [ ] Distributed tracing validation using OpenTelemetry

## References

- [System Architecture Document (SAD)](../../sad.md) - Section 10: Test Strategy
- [Tasks](../../tasks.yaml) - Epic 7, Task 3: E2E tests
- [Invoice Classifier Agent Definition](../../agents/definitions/invoice-classifier.json)
