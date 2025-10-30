# CI/CD Pipeline Testing Guide

This document provides guidance for testing the CI/CD pipeline implementation (E1-T11).

## Pre-Merge Testing

Before merging the PR, the following tests should be performed:

### 1. Workflow Syntax Validation ✅

**Status:** PASSED

All workflows have been validated using:
- Python YAML parser (syntax check)
- actionlint (GitHub Actions linter)

**Files validated:**
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `.github/workflows/code-quality.yml`

### 2. CI Pipeline Test (Automatic)

**Triggers:** This workflow will run automatically when the PR is created or updated.

**Expected Results:**
- ✅ .NET build and test job passes
- ✅ Next.js build and test job passes
- ✅ Docker images build successfully
- ✅ SBOMs are generated for all components
- ✅ Security scans complete without critical issues
- ✅ Artifacts are uploaded

**How to verify:**
1. Navigate to the PR in GitHub
2. Check the "Checks" tab
3. Look for the "CI Pipeline" workflow
4. Verify all jobs complete successfully

### 3. Code Quality Test (Automatic)

**Triggers:** Runs automatically on PR creation/update.

**Expected Results:**
- ✅ CodeQL analysis completes for C# and JavaScript
- ✅ Dependency review passes (or lists vulnerabilities)
- ✅ Secret scan completes
- ✅ Code formatting checks pass

**How to verify:**
1. Check the "Checks" tab in the PR
2. Look for "Code Quality and Security" workflow
3. Review any security findings in the Security tab

## Post-Merge Testing

After merging to `main`, verify the following:

### 4. Main Branch Build

**Expected Results:**
- ✅ CI pipeline runs on main branch
- ✅ Docker images are built and pushed to GHCR
- ✅ Images are signed with Cosign
- ✅ SBOMs are attested to images

**How to verify:**
```bash
# Check that images are available in GHCR
# (Requires authentication with GitHub token)
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:latest
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/node-runtime:latest
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/admin-ui:latest
```

### 5. Image Signature Verification

**How to verify:**
```bash
# Install cosign
curl -O -L "https://github.com/sigstore/cosign/releases/latest/download/cosign-linux-amd64"
chmod +x cosign-linux-amd64
sudo mv cosign-linux-amd64 /usr/local/bin/cosign

# Verify image signatures
cosign verify \
  --certificate-identity-regexp "https://github.com/dylan-mccarthy/Scalable-Process-Agent-System" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:latest

# Expected output: Signature verification successful with transparency log entry
```

### 6. SBOM Verification

**How to verify:**
```bash
# Download SBOM from workflow artifacts
# Navigate to Actions > CI Pipeline > Select latest run > Artifacts

# Or verify SBOM attestation on image
cosign verify-attestation \
  --type spdx \
  --certificate-identity-regexp "https://github.com/dylan-mccarthy/Scalable-Process-Agent-System" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:latest | jq
```

## Release Testing

### 7. Create a Test Release

**Steps:**
1. Create a version tag:
   ```bash
   git tag v0.1.0-test
   git push origin v0.1.0-test
   ```

2. Verify the release pipeline runs

3. Check that:
   - ✅ Images are tagged with semantic versions (v0.1.0-test, v0.1, v0)
   - ✅ All image tags are signed
   - ✅ SBOMs are attached to the GitHub Release
   - ✅ Security scans complete
   - ✅ Release summary is generated

**How to verify:**
```bash
# Verify semantic version tags exist
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:v0.1.0-test
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:v0.1
docker pull ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:v0

# Verify signatures on all tags
cosign verify \
  --certificate-identity-regexp "https://github.com/dylan-mccarthy/Scalable-Process-Agent-System" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  ghcr.io/dylan-mccarthy/scalable-process-agent-system/control-plane:v0.1.0-test
```

## Security Testing

### 8. Vulnerability Scanning Results

**How to review:**
1. Navigate to the repository Security tab
2. Check "Code scanning alerts" for CodeQL findings
3. Check "Dependabot alerts" for dependency vulnerabilities
4. Review Trivy scan results in workflow logs

### 9. SBOM Compliance Check

**Verify SBOM contents:**
```bash
# Download SBOM artifact from workflow
# Check that it contains:
# - Package names and versions
# - License information
# - Dependency relationships
# - SPDX format compliance

# Validate SBOM format
cat sbom-control-plane-image.spdx.json | jq .spdxVersion
# Expected: "SPDX-2.3" or similar
```

## Manual Test Checklist

- [ ] PR triggers CI pipeline automatically
- [ ] All CI jobs complete successfully
- [ ] Code quality checks run on PR
- [ ] Main branch merge triggers image build and push
- [ ] Images are available in GHCR
- [ ] Images are signed with Cosign
- [ ] Image signatures can be verified
- [ ] SBOMs are generated for source code
- [ ] SBOMs are generated for container images
- [ ] SBOMs are attested to images
- [ ] Release tag triggers release pipeline
- [ ] Semantic version tags are created
- [ ] SBOMs are attached to GitHub Release
- [ ] Security scans complete without blocking issues
- [ ] Documentation is accurate and complete

## Troubleshooting

### Issue: Cosign signing fails

**Possible causes:**
- Missing `id-token: write` permission
- COSIGN_EXPERIMENTAL not set to "true"
- Image not pushed before signing

**Solution:**
- Verify permissions in workflow file
- Check environment variables
- Ensure image push completes before signing step

### Issue: SBOM generation fails

**Possible causes:**
- Path to project/image is incorrect
- Insufficient disk space
- Network issues downloading dependencies

**Solution:**
- Verify paths in workflow
- Check disk space in logs
- Retry workflow

### Issue: Test failures

**Possible causes:**
- Code changes broke existing functionality
- External dependencies unavailable
- Test infrastructure issues

**Solution:**
- Review test logs for specific failures
- Check if dependencies (Redis, NATS) are available
- Run tests locally to reproduce

## Success Criteria

The CI/CD pipeline implementation is considered successful when:

1. ✅ All workflows pass syntax validation
2. ✅ CI pipeline runs successfully on every PR and push
3. ✅ SBOMs are generated in SPDX format
4. ✅ Container images are signed with Sigstore
5. ✅ Security scans complete and results are actionable
6. ✅ Release pipeline creates proper semantic versioning
7. ✅ Documentation is complete and accurate
8. ✅ No critical security vulnerabilities in builds

## Next Steps

After successful testing:

1. Merge the PR to main
2. Monitor the first main branch build
3. Verify images in GHCR
4. Create a test release (v0.1.0)
5. Update team documentation with CI/CD processes
6. Set up branch protection rules requiring CI checks
7. Configure required reviewers for workflow changes
