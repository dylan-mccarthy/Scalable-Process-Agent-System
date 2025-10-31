# mTLS Configuration Guide

This guide explains how to configure and use mutual TLS (mTLS) for secure gRPC communication between the Node Runtime and Control Plane API.

## Overview

Mutual TLS (mTLS) provides secure, encrypted communication with bidirectional certificate authentication:
- **Server Authentication**: Nodes validate the Control Plane's identity
- **Client Authentication**: Control Plane validates each Node's identity
- **Encrypted Communication**: All data is encrypted in transit

This implementation follows the security requirements defined in the System Architecture Document (SAD) section 4.6.

## Prerequisites

- OpenSSL (for certificate generation)
- .NET 9.0 SDK
- Access to both ControlPlane.Api and Node.Runtime projects

## Quick Start

### 1. Generate Certificates

Use the provided script to generate all required certificates:

```bash
cd /path/to/Scalable-Process-Agent-System
./scripts/generate-mtls-certs.sh
```

This creates a `certs/` directory with:
- `ca-cert.pem` - Certificate Authority certificate
- `ca-key.pem` - CA private key (keep secure!)
- `server-cert.pem` - Control Plane server certificate
- `server-key.pem` - Server private key (keep secure!)
- `node-cert.pem` - Node Runtime client certificate
- `node-key.pem` - Client private key (keep secure!)

**Important**: Never commit private keys (`*.key.pem`) to version control. Add them to `.gitignore`.

### 2. Configure Control Plane

Update `src/ControlPlane.Api/appsettings.Development.json`:

```json
{
  "MTls": {
    "Enabled": true,
    "ServerCertificatePath": "./certs/server-cert.pem",
    "ServerKeyPath": "./certs/server-key.pem",
    "ClientCaCertificatePath": "./certs/ca-cert.pem",
    "RequireClientCertificate": true,
    "ValidateCertificateChain": true,
    "AllowedClientCertificateSubjects": []
  }
}
```

### 3. Configure Node Runtime

Update `src/Node.Runtime/appsettings.Development.json`:

```json
{
  "NodeRuntime": {
    "ControlPlaneUrl": "https://localhost:5109"
  },
  "MTls": {
    "Enabled": true,
    "ClientCertificatePath": "./certs/node-cert.pem",
    "ClientKeyPath": "./certs/node-key.pem",
    "ServerCaCertificatePath": "./certs/ca-cert.pem",
    "ExpectedServerCertificateSubject": "control-plane",
    "ValidateCertificateChain": true
  }
}
```

**Note**: Change `ControlPlaneUrl` from `http://` to `https://` when enabling mTLS.

### 4. Start Services

```bash
# Terminal 1: Start Control Plane
cd src/ControlPlane.Api
dotnet run

# Terminal 2: Start Node Runtime
cd src/Node.Runtime
dotnet run
```

The Node Runtime should successfully connect to the Control Plane using mTLS.

## Configuration Reference

### Control Plane MTls Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Enable/disable mTLS for gRPC |
| `ServerCertificatePath` | string | null | Path to server certificate (PEM) |
| `ServerKeyPath` | string | null | Path to server private key (PEM) |
| `ClientCaCertificatePath` | string | null | Path to CA certificate for validating clients |
| `RequireClientCertificate` | bool | true | Require clients to present certificates |
| `ValidateCertificateChain` | bool | true | Validate certificate chain |
| `AllowedClientCertificateSubjects` | string[] | [] | Allowed client CN values (empty = any CA-signed cert) |

### Node Runtime MTls Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Enable/disable mTLS for gRPC |
| `ClientCertificatePath` | string | null | Path to client certificate (PEM) |
| `ClientKeyPath` | string | null | Path to client private key (PEM) |
| `ServerCaCertificatePath` | string | null | Path to CA certificate for validating server |
| `ExpectedServerCertificateSubject` | string | null | Expected server CN (optional validation) |
| `ValidateCertificateChain` | bool | true | Validate certificate chain |

## Production Deployment

### Kubernetes/AKS

1. **Create Kubernetes Secrets**:

```bash
kubectl create secret generic bpa-mtls-certs \
  --from-file=ca-cert.pem=./certs/ca-cert.pem \
  --from-file=server-cert.pem=./certs/server-cert.pem \
  --from-file=server-key.pem=./certs/server-key.pem \
  -n bpa-system

kubectl create secret generic bpa-node-mtls-certs \
  --from-file=ca-cert.pem=./certs/ca-cert.pem \
  --from-file=node-cert.pem=./certs/node-cert.pem \
  --from-file=node-key.pem=./certs/node-key.pem \
  -n bpa-system
```

2. **Mount Secrets in Helm Values**:

```yaml
controlPlane:
  mtls:
    enabled: true
    serverCertPath: /etc/bpa/certs/server-cert.pem
    serverKeyPath: /etc/bpa/certs/server-key.pem
    clientCaPath: /etc/bpa/certs/ca-cert.pem
  
  volumeMounts:
    - name: mtls-certs
      mountPath: /etc/bpa/certs
      readOnly: true
  
  volumes:
    - name: mtls-certs
      secret:
        secretName: bpa-mtls-certs

nodeRuntime:
  mtls:
    enabled: true
    clientCertPath: /etc/bpa/certs/node-cert.pem
    clientKeyPath: /etc/bpa/certs/node-key.pem
    serverCaPath: /etc/bpa/certs/ca-cert.pem
  
  volumeMounts:
    - name: mtls-certs
      mountPath: /etc/bpa/certs
      readOnly: true
  
  volumes:
    - name: mtls-certs
      secret:
        secretName: bpa-node-mtls-certs
```

### Azure Key Vault Integration

For production, use Azure Key Vault with External Secrets Operator:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: bpa-mtls-certs
  namespace: bpa-system
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: azure-key-vault
    kind: SecretStore
  target:
    name: bpa-mtls-certs
  data:
    - secretKey: ca-cert.pem
      remoteRef:
        key: bpa-ca-cert
    - secretKey: server-cert.pem
      remoteRef:
        key: bpa-server-cert
    - secretKey: server-key.pem
      remoteRef:
        key: bpa-server-key
```

## Certificate Rotation

### Development

Generate new certificates with the script and restart services:

```bash
./scripts/generate-mtls-certs.sh
# Restart Control Plane and Nodes
```

### Production

1. Generate new certificates with the same CA
2. Update Kubernetes secrets
3. Rolling restart pods to pick up new certificates

```bash
kubectl rollout restart deployment/control-plane -n bpa-system
kubectl rollout restart deployment/node-runtime -n bpa-system
```

## Troubleshooting

### Common Issues

**1. "Failed to load certificate" errors**

- Verify certificate paths are correct
- Ensure files are readable by the application process
- Check certificate format (must be PEM)

```bash
# Verify certificate format
openssl x509 -in server-cert.pem -text -noout
```

**2. "Certificate validation failed" errors**

- Verify CA certificate is correct
- Check certificate hasn't expired
- Ensure certificate chain is valid

```bash
# Verify certificate chain
openssl verify -CAfile ca-cert.pem server-cert.pem
openssl verify -CAfile ca-cert.pem node-cert.pem
```

**3. "Subject CN mismatch" errors**

- Verify `ExpectedServerCertificateSubject` matches server certificate CN
- Check server certificate subject:

```bash
openssl x509 -in server-cert.pem -noout -subject
```

**4. Connection refused / timeout**

- Ensure Control Plane is using HTTPS (not HTTP) when mTLS is enabled
- Verify firewall rules allow HTTPS traffic
- Check service DNS resolution

### Enable Debug Logging

Add to `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.Server.Kestrel": "Debug",
      "Grpc": "Debug"
    }
  }
}
```

### Verify Certificate Information

```bash
# View CA certificate
openssl x509 -in certs/ca-cert.pem -noout -text

# View server certificate
openssl x509 -in certs/server-cert.pem -noout -text

# View client certificate
openssl x509 -in certs/node-cert.pem -noout -text

# Verify certificate chain
openssl verify -CAfile certs/ca-cert.pem certs/server-cert.pem
openssl verify -CAfile certs/ca-cert.pem certs/node-cert.pem
```

## Security Best Practices

1. **Private Key Protection**
   - Never commit private keys to version control
   - Use file permissions to restrict access (600 or 400)
   - Store production keys in Azure Key Vault or similar

2. **Certificate Rotation**
   - Rotate certificates regularly (recommend every 90 days)
   - Use shorter validity periods for certificates
   - Implement automated rotation processes

3. **Certificate Validation**
   - Always enable chain validation in production
   - Use specific subject name validation when possible
   - Keep CA certificates secure

4. **Monitoring**
   - Monitor certificate expiration dates
   - Alert on certificate validation failures
   - Log all mTLS authentication attempts

## Testing

### Unit Tests

Run mTLS-specific tests:

```bash
dotnet test --filter "FullyQualifiedName~MTls"
```

### Integration Testing

The test suite includes integration tests that verify mTLS functionality. These tests:
- Generate temporary certificates
- Start test servers with mTLS
- Verify client-server communication
- Test certificate validation failures

## Architecture Alignment

This implementation aligns with SAD requirements:
- **Section 4.6 Security**: "mTLS for gRPC node links (K8s: service mesh or secret‑mounted certs)"
- **Section 8 NFRs**: Secrets only in AKV; mTLS on node links
- Configurable via appsettings for different environments
- Production-ready with Kubernetes secret management

## References

- [System Architecture Document (SAD)](../sad.md) - Section 4.6 Security
- [ASP.NET Core Kestrel HTTPS Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)
- [gRPC Client Configuration](https://learn.microsoft.com/en-us/aspnet/core/grpc/client)
- [OpenSSL Documentation](https://www.openssl.org/docs/)
