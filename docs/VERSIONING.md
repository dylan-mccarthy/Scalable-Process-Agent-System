# Agent Versioning and Validation

## Overview

The Control Plane API provides comprehensive versioning capabilities for agent definitions, ensuring that each version is semantically valid and contains properly configured agent specifications.

## Versioning Endpoints

### Create Agent Version

Creates a new version of an agent definition.

**Endpoint:** `POST /v1/agents/{agentId}:version`

**Request Body:**
```json
{
  "version": "1.0.0",
  "spec": {
    "agentId": "invoice-classifier",
    "name": "Invoice Classifier",
    "description": "Classifies vendor invoices",
    "instructions": "Classify vendor + route to appropriate API endpoint",
    "modelProfile": {
      "model": "gpt-4"
    },
    "budget": {
      "maxTokens": 4000,
      "maxDurationSeconds": 60
    },
    "tools": ["http-post"],
    "input": {
      "type": "service-bus",
      "config": {
        "queue": "invoices"
      }
    },
    "output": {
      "type": "http",
      "config": {
        "baseUrl": "https://api.example.com/invoices"
      }
    },
    "metadata": {
      "team": "finance",
      "environment": "production"
    }
  }
}
```

**Response:** `201 Created`
```json
{
  "agentId": "invoice-classifier",
  "version": "1.0.0",
  "spec": { ... },
  "createdAt": "2025-10-30T23:00:00Z"
}
```

### Get Agent Versions

Lists all versions of an agent, ordered by creation date (newest first).

**Endpoint:** `GET /v1/agents/{agentId}/versions`

**Response:** `200 OK`
```json
[
  {
    "agentId": "invoice-classifier",
    "version": "2.0.0",
    "spec": { ... },
    "createdAt": "2025-10-30T23:00:00Z"
  },
  {
    "agentId": "invoice-classifier",
    "version": "1.0.0",
    "spec": { ... },
    "createdAt": "2025-10-29T10:00:00Z"
  }
]
```

### Get Specific Version

Retrieves a specific version of an agent.

**Endpoint:** `GET /v1/agents/{agentId}/versions/{version}`

**Response:** `200 OK` or `404 Not Found`

### Delete Version

Deletes a specific version of an agent.

**Endpoint:** `DELETE /v1/agents/{agentId}/versions/{version}`

**Response:** `204 No Content` or `404 Not Found`

## Version Format Validation

Versions must follow **Semantic Versioning 2.0.0** format:

- **Format:** `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`
- **Valid Examples:**
  - `1.0.0`
  - `2.3.4`
  - `1.0.0-alpha`
  - `1.0.0-beta.1`
  - `1.0.0-rc.1+build.123`
  - `2.0.0+20231030`

- **Invalid Examples:**
  - `1.0` (missing patch)
  - `v1.0.0` (no prefix allowed)
  - `1.0.0.0` (too many segments)
  - `1.0.0-` (incomplete prerelease)

## Agent Specification Validation

When creating a version with a spec, the following validations are enforced:

### Required Fields

- **name**: Agent name (required, non-empty)
- **instructions**: Agent instructions (required, non-empty)

### Budget Constraints

If a budget is specified:

- **maxTokens**
  - Must be > 0
  - Cannot exceed 128,000 tokens
  - Enforces token limits per execution

- **maxDurationSeconds**
  - Must be > 0
  - Cannot exceed 3,600 seconds (1 hour)
  - Enforces time limits per execution

### Connector Configuration

If input or output connectors are specified:

- **type**: Required, must be one of:
  - `service-bus` - Azure Service Bus
  - `http` - HTTP/REST API
  - `kafka` - Apache Kafka
  - `storage` - Azure Storage (Blob/Queue)
  - `sql` - SQL Database

- **config**: Optional configuration dictionary specific to connector type

### Tools

If tools are specified:

- Tool names must not be empty or whitespace
- No duplicate tool names allowed
- Tool names are case-sensitive

### Null Specs

Versions can be created with `null` spec to represent a version without specification changes. This is useful for tracking version metadata separately from the agent definition.

## Validation Error Responses

When validation fails, the API returns `400 Bad Request` with detailed error information:

```json
{
  "error": "Spec validation failed",
  "errors": [
    "Agent name is required",
    "MaxTokens must be greater than 0",
    "Input connector type 'invalid-type' is not recognized"
  ]
}
```

## Best Practices

1. **Use Semantic Versioning**
   - Increment MAJOR for breaking changes
   - Increment MINOR for new features
   - Increment PATCH for bug fixes

2. **Validate Before Creating**
   - Ensure all required fields are populated
   - Test budget constraints align with expected workload
   - Verify connector types match your infrastructure

3. **Document Changes**
   - Use metadata fields to track change context
   - Include team/owner information
   - Tag with environment (dev, staging, production)

4. **Version Management**
   - Keep versions immutable once deployed
   - Delete only unused/invalid versions
   - Maintain version history for audit trails

## Example: Creating an Invoice Classifier Version

```bash
# Create the agent first
curl -X POST http://localhost:5000/v1/agents \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Invoice Classifier",
    "instructions": "Classify vendor invoices"
  }'

# Create version 1.0.0
curl -X POST http://localhost:5000/v1/agents/{agentId}:version \
  -H "Content-Type: application/json" \
  -d '{
    "version": "1.0.0",
    "spec": {
      "agentId": "invoice-classifier",
      "name": "Invoice Classifier",
      "instructions": "Classify vendor + route",
      "budget": {
        "maxTokens": 4000,
        "maxDurationSeconds": 60
      },
      "input": {
        "type": "service-bus"
      },
      "output": {
        "type": "http"
      }
    }
  }'
```

## Error Handling

| Status Code | Description |
|-------------|-------------|
| 201 Created | Version created successfully |
| 400 Bad Request | Invalid version format or spec validation failed |
| 404 Not Found | Agent or version not found |
| 409 Conflict | Version already exists or agent doesn't exist |

## See Also

- [System Architecture Document (SAD)](../sad.md)
- [Agent Definition Model](../src/ControlPlane.Api/Models/Agent.cs)
- [Version Validator](../src/ControlPlane.Api/Services/VersionValidator.cs)
- [Agent Spec Validator](../src/ControlPlane.Api/Services/AgentSpecValidator.cs)
