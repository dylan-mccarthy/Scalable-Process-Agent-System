# Authentication Setup Guide

This guide explains how to configure and use authentication for the Control Plane API.

## Overview

The Control Plane API supports OIDC (OpenID Connect) authentication with JWT Bearer tokens. The system is designed to work with:
- **Development**: Keycloak
- **Production**: Microsoft Entra ID (Azure AD)

Authentication is **disabled by default** and can be enabled via configuration.

## Quick Start with Keycloak (Development)

### 1. Start Keycloak and Dependencies

Start the development stack including Keycloak:

```bash
docker-compose -f docker-compose.dev.yml up -d
```

This starts:
- PostgreSQL (port 5432)
- Redis (port 6379)
- NATS (port 4222)
- Keycloak (port 8080)

Wait for Keycloak to be ready (may take 30-60 seconds on first start).

### 2. Configure Keycloak

Access Keycloak admin console at http://localhost:8080

**Default credentials:**
- Username: `admin`
- Password: `admin`

**Create a realm:**
1. Click "Create Realm"
2. Name: `bpa`
3. Click "Create"

**Create a client:**
1. In the `bpa` realm, go to "Clients"
2. Click "Create client"
3. Client ID: `control-plane-api`
4. Client Protocol: `openid-connect`
5. Click "Next"
6. Enable "Client authentication"
7. Enable "Authorization"
8. Valid Redirect URIs: `http://localhost:*`
9. Web Origins: `http://localhost:*`
10. Click "Save"

**Create a test user:**
1. Go to "Users" → "Add user"
2. Username: `testuser`
3. Click "Create"
4. Go to "Credentials" tab
5. Set password: `testpass`
6. Disable "Temporary"
7. Click "Set password"

### 3. Enable Authentication in API

Update `appsettings.Development.json`:

```json
{
  "Authentication": {
    "Enabled": true,
    "Provider": "Keycloak",
    "Authority": "http://localhost:8080/realms/bpa",
    "Audience": "control-plane-api",
    "RequireHttpsMetadata": false,
    "ValidateIssuer": true,
    "ValidateAudience": true
  }
}
```

### 4. Run the API

```bash
cd src/ControlPlane.Api
dotnet run
```

### 5. Get an Access Token

Use the Keycloak token endpoint to get a JWT:

```bash
curl -X POST http://localhost:8080/realms/bpa/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=control-plane-api" \
  -d "client_secret=<your-client-secret>" \
  -d "username=testuser" \
  -d "password=testpass"
```

**Note:** Get the client secret from Keycloak Admin Console → Clients → control-plane-api → Credentials tab.

### 6. Call the API with Token

```bash
export TOKEN="<access_token_from_previous_step>"

curl http://localhost:5000/v1/agents \
  -H "Authorization: Bearer $TOKEN"
```

## Configuration Reference

### Authentication Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Enable/disable authentication |
| `Provider` | string | "Keycloak" | Provider name (Keycloak, EntraId) |
| `Authority` | string | "" | OIDC authority URL |
| `Audience` | string | "" | Expected audience in JWT |
| `RequireHttpsMetadata` | bool | true | Require HTTPS for metadata endpoint |
| `MetadataAddress` | string | null | Optional metadata endpoint override |
| `ValidateIssuer` | bool | true | Validate token issuer |
| `ValidIssuers` | string[] | null | Optional list of valid issuers |
| `ValidateAudience` | bool | true | Validate token audience |
| `ValidAudiences` | string[] | null | Optional list of valid audiences |

### Example: Production with Entra ID

```json
{
  "Authentication": {
    "Enabled": true,
    "Provider": "EntraId",
    "Authority": "https://login.microsoftonline.com/<tenant-id>",
    "Audience": "api://control-plane-api",
    "RequireHttpsMetadata": true,
    "ValidateIssuer": true,
    "ValidIssuers": ["https://login.microsoftonline.com/<tenant-id>/v2.0"],
    "ValidateAudience": true
  }
}
```

## Development Workflow

### Disable Authentication for Local Testing

Set `Authentication:Enabled` to `false` in `appsettings.Development.json`:

```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

This allows testing endpoints without authentication.

### Testing with Authentication Enabled

The test suite includes authentication tests. Run them with:

```bash
dotnet test --filter "FullyQualifiedName~AuthenticationTests"
```

## Troubleshooting

### Common Issues

**1. "Failed to validate token" error**
- Verify Authority URL is correct and accessible
- Check that Keycloak realm name matches
- Ensure client ID matches the Audience

**2. "Invalid issuer" error**
- Check `ValidateIssuer` setting
- Verify the issuer claim in the JWT matches the authority

**3. "Invalid audience" error**
- Verify the `aud` claim in the JWT matches the configured Audience
- Check client configuration in Keycloak

**4. Keycloak not responding**
- Check if Keycloak container is running: `docker ps`
- View logs: `docker logs bpa-keycloak`
- Keycloak takes 30-60 seconds to start initially

### Debug Logging

Enable detailed authentication logs:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.IdentityModel": "Debug"
    }
  }
}
```

## Security Best Practices

1. **Production**: Always use HTTPS (`RequireHttpsMetadata: true`)
2. **Secrets**: Store client secrets in Azure Key Vault, not in configuration files
3. **Token Lifetime**: Use short-lived tokens (5-15 minutes recommended)
4. **Validation**: Keep issuer and audience validation enabled
5. **Logging**: Log authentication failures but never log tokens

## Architecture Alignment

This implementation follows the SAD requirements:
- OIDC for dev (Keycloak) and prod (Entra ID)
- Configurable authentication via appsettings
- Minimal changes to existing endpoints
- Ready for future authorization policies

## Next Steps

Future enhancements may include:
- Endpoint-level authorization with `[Authorize]` attributes
- Role-based access control (RBAC)
- API key support for service-to-service communication
- mTLS for gRPC node communication
