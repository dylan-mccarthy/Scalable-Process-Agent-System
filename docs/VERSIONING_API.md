# Agent Versioning API Examples

This document demonstrates the agent versioning endpoints implemented in E3-T2.

## Prerequisites
- Agent must exist before creating versions
- All versions must follow semantic versioning (SemVer 2.0.0)

## Create an Agent

```http
POST /v1/agents
Content-Type: application/json

{
  "name": "Invoice Classifier",
  "description": "Classifies invoices by vendor",
  "instructions": "Analyze invoice content and classify by vendor type",
  "modelProfile": {
    "model": "gpt-4",
    "temperature": 0.7
  },
  "budget": {
    "maxTokens": 4000,
    "maxDurationSeconds": 60
  }
}
```

Response:
```json
{
  "agentId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Invoice Classifier",
  ...
}
```

## Create Agent Version

```http
POST /v1/agents/550e8400-e29b-41d4-a716-446655440000:version
Content-Type: application/json

{
  "version": "1.0.0",
  "spec": {
    "agentId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Invoice Classifier",
    "description": "Classifies invoices by vendor",
    "instructions": "Analyze invoice content and classify by vendor type",
    "modelProfile": {
      "model": "gpt-4",
      "temperature": 0.7
    },
    "budget": {
      "maxTokens": 4000,
      "maxDurationSeconds": 60
    }
  }
}
```

Response (201 Created):
```json
{
  "agentId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "spec": { ... },
  "createdAt": "2025-10-30T22:30:00Z"
}
```

## List All Versions

```http
GET /v1/agents/550e8400-e29b-41d4-a716-446655440000/versions
```

Response (200 OK):
```json
[
  {
    "agentId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "2.0.0",
    "spec": { ... },
    "createdAt": "2025-10-30T23:00:00Z"
  },
  {
    "agentId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "1.1.0",
    "spec": { ... },
    "createdAt": "2025-10-30T22:45:00Z"
  },
  {
    "agentId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "1.0.0",
    "spec": { ... },
    "createdAt": "2025-10-30T22:30:00Z"
  }
]
```

## Get Specific Version

```http
GET /v1/agents/550e8400-e29b-41d4-a716-446655440000/versions/1.0.0
```

Response (200 OK):
```json
{
  "agentId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "spec": { ... },
  "createdAt": "2025-10-30T22:30:00Z"
}
```

## Delete Version

```http
DELETE /v1/agents/550e8400-e29b-41d4-a716-446655440000/versions/1.0.0
```

Response: 204 No Content

## Semantic Versioning Examples

### Valid Versions
- `1.0.0` - Standard version
- `0.1.0` - Initial development
- `1.2.3` - Patch release
- `1.0.0-alpha` - Pre-release
- `1.0.0-beta.1` - Pre-release with identifier
- `1.0.0-rc.1+build.123` - Pre-release with build metadata
- `2.0.0+20231030` - Release with build metadata

### Invalid Versions (400 Bad Request)
- `1.0` - Missing patch version
- `v1.0.0` - Prefix not allowed
- `1.0.0.0` - Too many version parts
- `1.0.0-` - Incomplete pre-release
- `01.0.0` - Leading zeros not allowed

## Error Responses

### 400 Bad Request - Invalid Version Format
```json
{
  "error": "Version 'v1.0.0' is not a valid semantic version. Expected format: MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]"
}
```

### 404 Not Found - Version Doesn't Exist
```json
{
  "status": 404
}
```

### 409 Conflict - Duplicate Version
```json
{
  "error": "Version 1.0.0 already exists for agent 550e8400-e29b-41d4-a716-446655440000"
}
```

### 409 Conflict - Agent Doesn't Exist
```json
{
  "error": "Agent with ID nonexistent-id does not exist"
}
```

## Integration with Deployments

Versions can be referenced in deployment configurations:

```json
{
  "agentId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "environment": "production",
  "target": {
    "slotBudget": 4,
    "resources": {
      "cpu": "500m",
      "memory": "1Gi"
    }
  }
}
```
