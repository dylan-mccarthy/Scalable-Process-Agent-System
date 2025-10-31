# mTLS (Mutual TLS) Implementation Summary

## Task: E2-T10 - Secure Communication

### Overview

This implementation adds mutual TLS (mTLS) support for secure gRPC communication between Node Runtime and Control Plane API, as specified in the System Architecture Document (SAD) section 4.6 Security.

### What Was Implemented

#### 1. Configuration Classes

**ControlPlane.Api/Models/MTlsOptions.cs**
- Server-side mTLS configuration
- Certificate paths for server cert, private key, and client CA
- Client certificate validation options
- Subject name filtering for additional security

**Node.Runtime/Configuration/MTlsOptions.cs**
- Client-side mTLS configuration  
- Certificate paths for client cert, private key, and server CA
- Server certificate validation options
- Expected server subject validation

#### 2. Server-Side Implementation (ControlPlane.Api)

**Program.cs modifications:**
- Added Kestrel HTTPS configuration with client certificate requirement
- Implemented custom client certificate validation callback
- Certificate chain validation against trusted CA
- Optional subject name validation for granular access control
- Comprehensive logging for certificate validation

**Features:**
- Load server certificates from PEM files
- Require and validate client certificates
- Validate certificate chains using custom CA
- Filter allowed clients by certificate subject (CN)
- Graceful degradation when mTLS is disabled

#### 3. Client-Side Implementation (Node.Runtime)

**Program.cs modifications:**
- Added HttpClientHandler with client certificate
- Implemented custom server certificate validation callback
- Certificate chain validation against server CA
- Optional server subject name validation
- HttpClient configured for gRPC channel

**Features:**
- Load client certificates from PEM files
- Validate server certificate against custom CA
- Verify server identity via subject name
- Support both mTLS and standard TLS modes

#### 4. Certificate Management

**scripts/generate-mtls-certs.sh**
- Automated certificate generation for development
- Creates CA, server, and client certificates
- Configurable validity period and subjects
- SubjectAlternativeName support for localhost and Kubernetes
- Certificate verification output

**Features:**
- Self-signed CA generation
- Server certificate with SAN for multiple hostnames
- Client certificate for node authentication
- All certificates in PEM format for compatibility
- Clean verification and documentation

#### 5. Documentation

**docs/MTLS_SETUP.md**
- Comprehensive setup guide
- Quick start instructions
- Configuration reference
- Production deployment guidance (Kubernetes/AKS)
- Azure Key Vault integration
- Troubleshooting section
- Security best practices

#### 6. Security Features

**Certificate Validation:**
- Full X.509 certificate chain validation
- Custom CA trust anchors
- Revocation checking (configurable)
- Subject/Issuer validation
- Expiration checking

**Access Control:**
- Client certificate requirement (configurable)
- Subject name filtering (optional)
- Chain validation (configurable)
- Mutual authentication

#### 7. Configuration Updates

**appsettings.json (both projects)**
- Added MTls configuration section
- Disabled by default for backward compatibility
- Example paths for production deployment
- Fully documented configuration options

**.gitignore**
- Added certificate file patterns
- Private key protection
- Development certificate exclusion

#### 8. Testing

**tests/ControlPlane.Api.Tests/MTlsIntegrationTests.cs**
- Certificate generation script verification
- Configuration options testing
- Certificate validation testing (platform-specific)
- 8 comprehensive tests (3 skipped in CI, runnable locally)

### Usage

#### Development

```bash
# Generate certificates
./scripts/generate-mtls-certs.sh

# Update appsettings.Development.json for both projects
# Enable MTls, set certificate paths

# Start services
dotnet run --project src/ControlPlane.Api
dotnet run --project src/Node.Runtime
```

#### Production (Kubernetes)

```bash
# Store certificates in Kubernetes secrets
kubectl create secret generic bpa-mtls-certs ...

# Update Helm values to mount secrets
# Deploy with Helm
helm install bpa ./helm/business-process-agents
```

### Security Considerations

1. **Private Key Protection**: All private keys (.key.pem) are excluded from version control
2. **Certificate Rotation**: Documented process for rotating certificates in production
3. **Validation**: Full chain validation with custom CA support
4. **Logging**: Comprehensive logging for security auditing
5. **Backward Compatibility**: mTLS disabled by default, opt-in feature

### Architecture Alignment

This implementation aligns with:
- **SAD Section 4.6**: "mTLS for gRPC node links (K8s: service mesh or secret‑mounted certs)"
- **SAD Section 8 NFRs**: "Secrets only in AKV; mTLS on node links; RBAC for UI"
- **Tasks.yaml E2-T10**: "Enable mTLS between node and control plane"

### Test Results

- **Total Tests**: 376
- **Passed**: 373
- **Skipped**: 3 (platform-specific cert generation tests)
- **Failed**: 0
- **CodeQL Security Scan**: 0 issues found

### Files Changed

#### New Files
- `src/ControlPlane.Api/Models/MTlsOptions.cs`
- `src/Node.Runtime/Configuration/MTlsOptions.cs`
- `scripts/generate-mtls-certs.sh`
- `docs/MTLS_SETUP.md`
- `tests/ControlPlane.Api.Tests/MTlsIntegrationTests.cs`

#### Modified Files
- `src/ControlPlane.Api/Program.cs` (mTLS configuration)
- `src/Node.Runtime/Program.cs` (mTLS client configuration)
- `src/ControlPlane.Api/appsettings.json` (MTls section)
- `src/Node.Runtime/appsettings.json` (MTls section)
- `.gitignore` (certificate exclusions)

### Next Steps (Post-MVP)

- Service mesh integration (Istio/Linkerd) for automatic mTLS
- Certificate rotation automation
- Certificate metrics and expiration alerts
- Short-lived certificates with automatic renewal
- Hardware security module (HSM) integration for CA keys

### References

- [System Architecture Document](../sad.md)
- [mTLS Setup Guide](../docs/MTLS_SETUP.md)
- [Tasks Definition](../tasks.yaml)
