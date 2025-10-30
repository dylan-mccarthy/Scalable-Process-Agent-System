# CI/CD Pipeline Documentation

This document describes the CI/CD pipeline for the Business Process Agents MVP platform (E1-T11).

## Overview

The CI/CD pipeline provides comprehensive build, test, security scanning, SBOM generation, and container image signing capabilities. It consists of three main workflows:

1. **CI Pipeline** (`ci.yml`) - Runs on every push and pull request
2. **Release Pipeline** (`release.yml`) - Runs on version tags and releases
3. **Code Quality and Security** (`code-quality.yml`) - Runs on PRs, main pushes, and weekly

## Workflows

### 1. CI Pipeline (`ci.yml`)

**Triggers:**
- Push to `main` branch
- Push to feature, bugfix, and hotfix branches
- Pull requests to `main`

**Jobs:**

#### a. .NET Build and Test
- Restores dependencies
- Builds the solution in Release configuration
- Runs all unit and integration tests
- Collects code coverage
- Uploads test results and coverage artifacts
- **Generates SBOM** for Control Plane API and Node Runtime using Anchore SBOM Action

#### b. Next.js Build and Test
- Installs Node.js dependencies
- Runs ESLint for code quality
- Builds the Admin UI application
- Runs tests (if present)
- **Generates SBOM** for Admin UI using Anchore SBOM Action

#### c. Docker Build and Scan
- Builds container images for all three components:
  - Control Plane API
  - Node Runtime
  - Admin UI
- **Scans images for vulnerabilities** using Trivy
- Uploads security scan results to GitHub Security tab
- **Generates SBOMs** for container images
- **Pushes images** to GitHub Container Registry (main branch only)
- **Signs images** with Sigstore/Cosign using keyless signing
- **Attests SBOMs** to signed images

#### d. CI Summary
- Aggregates results from all jobs
- Reports overall pipeline status

### 2. Release Pipeline (`release.yml`)

**Triggers:**
- Push of version tags (e.g., `v1.0.0`)
- GitHub Release publication

**Jobs:**

#### a. Build and Publish Release
- Builds production container images
- Generates semantic version tags (major, minor, patch, latest)
- **Pushes images** to GitHub Container Registry with all version tags
- Includes **provenance** and **SBOM** metadata (built-in Docker Buildx features)
- **Scans images** for vulnerabilities with Trivy
- **Generates comprehensive SBOMs** using Anchore
- **Signs images** with Cosign using keyless signing
- **Attests SBOMs** to all image tags
- **Attaches SBOMs** to GitHub Release as downloadable artifacts

#### b. Release Summary
- Downloads all generated SBOMs
- Creates a comprehensive release summary
- Provides verification instructions

### 3. Code Quality and Security (`code-quality.yml`)

**Triggers:**
- Pull requests to `main`
- Push to `main` branch
- Weekly schedule (Sundays at midnight UTC)

**Jobs:**

#### a. CodeQL Analysis (.NET)
- Performs static code analysis for C# code
- Identifies security vulnerabilities and code quality issues
- Uses `security-and-quality` query suite

#### b. CodeQL Analysis (JavaScript)
- Performs static code analysis for JavaScript/TypeScript
- Identifies security vulnerabilities in Next.js code

#### c. Dependency Review
- Reviews dependency changes in pull requests
- Alerts on vulnerable dependencies
- Fails on moderate or higher severity vulnerabilities
- Posts summary to PR comments

#### d. Secret Scanning
- Scans for accidentally committed secrets
- Uses TruffleHog for comprehensive secret detection
- Only reports verified secrets to reduce false positives

#### e. .NET Code Quality
- Verifies code formatting compliance
- Generates detailed code coverage reports
- Uses ReportGenerator for coverage visualization

#### f. Next.js Code Quality
- Runs ESLint for code style
- Performs TypeScript type checking
- Audits npm dependencies for vulnerabilities

## SBOM Generation

### What is an SBOM?

A Software Bill of Materials (SBOM) is a complete, formally structured list of components, libraries, and dependencies in a software application. It provides transparency into what makes up your software.

### SBOM Formats

We generate SBOMs in **SPDX JSON** format, which is:
- An industry-standard format
- Supported by most security tools
- Human-readable and machine-parseable
- Compliant with US Executive Order 14028

### SBOM Generation Points

1. **Source Code SBOMs** (CI Pipeline)
   - Generated for .NET projects (Control Plane API, Node Runtime)
   - Generated for Node.js project (Admin UI)
   - Uploaded as workflow artifacts

2. **Container Image SBOMs** (CI and Release Pipelines)
   - Generated for all container images
   - Includes both application and OS-level dependencies
   - Attested and signed with container images

### SBOM Tools

We use **Anchore SBOM Action** (`anchore/sbom-action`):
- Industry-leading SBOM generation tool
- Supports multiple formats (SPDX, CycloneDX)
- Analyzes both source code and container images
- Integrates seamlessly with GitHub Actions

## Container Image Signing

### Why Sign Images?

Image signing provides:
- **Verification** that images are from a trusted source
- **Integrity** assurance that images haven't been tampered with
- **Non-repudiation** proof of who published the image

### Signing Technology: Sigstore/Cosign

We use **Cosign** from the Sigstore project:
- **Keyless signing** - No need to manage private keys
- Uses OIDC identity from GitHub Actions
- Signatures stored in the OCI registry alongside images
- Transparent, public signature log (Rekor)

### How to Verify Signatures

```bash
# Install Cosign
curl -O -L "https://github.com/sigstore/cosign/releases/latest/download/cosign-linux-amd64"
chmod +x cosign-linux-amd64
sudo mv cosign-linux-amd64 /usr/local/bin/cosign

# Verify an image signature
cosign verify \
  --certificate-identity-regexp "https://github.com/dylan-mccarthy/Scalable-Process-Agent-System" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:latest

# Verify SBOM attestation
cosign verify-attestation \
  --type spdx \
  --certificate-identity-regexp "https://github.com/dylan-mccarthy/Scalable-Process-Agent-System" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:latest
```

### What Gets Signed?

1. **Container Images**: All three images (Control Plane, Node Runtime, Admin UI)
2. **SBOM Attestations**: SBOMs are attested and signed with the images
3. **All Tags**: Every tag (latest, version tags, branch tags) is signed

## Security Scanning

### Trivy

We use **Trivy** by Aqua Security for vulnerability scanning:
- Scans container images for known vulnerabilities
- Checks OS packages and application dependencies
- Generates SARIF reports for GitHub Security tab
- Runs on every build and release

### CodeQL

GitHub CodeQL performs static analysis:
- Identifies security vulnerabilities in code
- Detects code quality issues
- Supports both C# and JavaScript/TypeScript
- Results appear in GitHub Security tab

### Dependency Review

GitHub Dependency Review:
- Checks for vulnerable dependencies in PRs
- Fails builds on moderate+ severity issues
- Posts detailed reports in PR comments

### Secret Scanning

TruffleHog scans for secrets:
- Checks all commits for leaked credentials
- Only reports verified secrets
- Runs on every push and PR

## Container Registry

Images are pushed to **GitHub Container Registry (ghcr.io)**:

**Image URLs:**
```
ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane
ghcr.io/dylan-mccarthy/scalable-process-agent-system/node-runtime
ghcr.io/dylan-mccarthy/scalable-process-agent-system/admin-ui
```

**Available Tags:**
- `latest` - Latest from main branch
- `v1.0.0`, `v1.0`, `v1` - Semantic version tags (releases only)
- `main-<sha>` - Main branch with commit SHA
- `pr-<number>` - Pull request builds

## Artifacts

### Workflow Artifacts

Available as GitHub Actions artifacts:

1. **Test Results**
   - .NET test results (TRX format)
   - Test coverage reports

2. **SBOMs**
   - Source code SBOMs (Control Plane, Node Runtime, Admin UI)
   - Container image SBOMs

3. **Coverage Reports**
   - Code coverage reports (HTML and Cobertura)

4. **Security Scan Results**
   - Trivy SARIF reports (in Security tab)
   - CodeQL results (in Security tab)

### Release Artifacts

Attached to GitHub Releases:
- SBOMs for all container images
- Signed and verifiable with Cosign

## Permissions

The workflows require the following permissions:

### CI Pipeline
- `contents: read` - Read repository code
- `packages: write` - Push to GitHub Container Registry
- `id-token: write` - Sigstore keyless signing
- `security-events: write` - Upload security scan results

### Release Pipeline
- Same as CI Pipeline
- `contents: write` - Attach files to releases

### Code Quality
- `actions: read` - Access workflow logs
- `contents: read` - Read repository code
- `security-events: write` - Upload CodeQL results
- `pull-requests: write` - Comment on PRs

## Environment Variables

Key environment variables:
- `DOTNET_VERSION: '9.0'` - .NET SDK version
- `NODE_VERSION: '20'` - Node.js version
- `REGISTRY: ghcr.io` - Container registry

## Best Practices

1. **Always review security scan results** before merging PRs
2. **Verify image signatures** before deploying to production
3. **Keep dependencies up to date** to avoid vulnerabilities
4. **Use semantic versioning** for releases (e.g., v1.2.3)
5. **Review SBOMs** to understand component dependencies
6. **Monitor the Security tab** for new vulnerabilities

## Compliance

This CI/CD pipeline helps meet:
- **US Executive Order 14028** (Software Supply Chain Security)
  - SBOM generation and distribution
  - Cryptographic signing of artifacts
- **NIST SSDF** (Secure Software Development Framework)
  - Automated security testing
  - Dependency management
- **SLSA Level 2** (Supply-chain Levels for Software Artifacts)
  - Provenance generation
  - Build service authentication

## Troubleshooting

### Build Failures

**Problem:** .NET build fails
- Check that all dependencies are properly restored
- Ensure .NET SDK version matches project requirements
- Review build logs for specific errors

**Problem:** Docker build fails
- Check Dockerfile syntax
- Ensure base images are accessible
- Verify context paths are correct

### Signing Failures

**Problem:** Cosign signing fails
- Ensure `COSIGN_EXPERIMENTAL: "true"` is set
- Verify `id-token: write` permission is granted
- Check that image was successfully pushed

### SBOM Generation Failures

**Problem:** SBOM generation fails
- Verify Anchore action version is current
- Check that the target path/image exists
- Ensure output directory is writable

## Future Enhancements

Potential improvements for the CI/CD pipeline:

1. **Advanced Testing**
   - Integration tests with real Azure services
   - Performance benchmarking
   - Load testing

2. **Enhanced Security**
   - Software Composition Analysis (SCA)
   - Dynamic Application Security Testing (DAST)
   - Container runtime security policies

3. **Deployment Automation**
   - Automated deployment to staging on main merge
   - GitOps-based deployment with ArgoCD
   - Blue-green or canary deployments

4. **Observability**
   - Build metrics and analytics
   - Pipeline performance monitoring
   - Alert on security findings

5. **Compliance**
   - SLSA Level 3 provenance
   - Enhanced audit logging
   - Compliance reporting automation

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Sigstore/Cosign](https://docs.sigstore.dev/)
- [Anchore SBOM Action](https://github.com/anchore/sbom-action)
- [Trivy](https://github.com/aquasecurity/trivy)
- [SPDX Specification](https://spdx.dev/)
- [SLSA Framework](https://slsa.dev/)
- [US EO 14028](https://www.whitehouse.gov/briefing-room/presidential-actions/2021/05/12/executive-order-on-improving-the-nations-cybersecurity/)
