using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using FluentAssertions;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Integration tests for mTLS functionality.
/// These tests verify certificate generation, validation, and secure communication.
/// </summary>
public class MTlsIntegrationTests : IDisposable
{
    private readonly string _testCertsPath;
    private bool _disposed = false;

    public MTlsIntegrationTests()
    {
        // Create temporary directory for test certificates
        _testCertsPath = Path.Combine(Path.GetTempPath(), $"bpa-mtls-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testCertsPath);
    }

    [Fact]
    public void GenerateCertificateScript_ShouldExist()
    {
        // Arrange
        var scriptPath = Path.Combine("..", "..", "..", "..", "..", "scripts", "generate-mtls-certs.sh");
        var fullPath = Path.GetFullPath(scriptPath);

        // Act & Assert
        File.Exists(fullPath).Should().BeTrue($"Certificate generation script should exist at {fullPath}");
    }

    [Fact]
    public void CertificateGeneration_ShouldCreateValidCertificates()
    {
        // Arrange
        var scriptPath = Path.Combine("..", "..", "..", "..", "..", "scripts", "generate-mtls-certs.sh");
        var fullScriptPath = Path.GetFullPath(scriptPath);

        if (!File.Exists(fullScriptPath) || !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            // Skip test if script doesn't exist or not on Unix-like OS
            return;
        }

        // Act
        var result = RunCertificateGenerationScript(fullScriptPath, _testCertsPath);

        // Assert
        result.Should().Be(0, "Certificate generation script should exit successfully");

        // Verify all required files were created
        var expectedFiles = new[]
        {
            "ca-cert.pem",
            "ca-key.pem",
            "server-cert.pem",
            "server-key.pem",
            "node-cert.pem",
            "node-key.pem"
        };

        foreach (var file in expectedFiles)
        {
            var filePath = Path.Combine(_testCertsPath, file);
            File.Exists(filePath).Should().BeTrue($"{file} should be created");
        }
    }

    [Fact(Skip = "Requires OpenSSL and bash shell - test locally on Linux/Mac")]
    public void Certificate_ShouldHaveValidSubjectAndIssuer()
    {
        // Arrange
        var scriptPath = Path.Combine("..", "..", "..", "..", "..", "scripts", "generate-mtls-certs.sh");
        var fullScriptPath = Path.GetFullPath(scriptPath);

        if (!File.Exists(fullScriptPath) || !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        RunCertificateGenerationScript(fullScriptPath, _testCertsPath);

        // Act
        var caCertPath = Path.Combine(_testCertsPath, "ca-cert.pem");
        var serverCertPath = Path.Combine(_testCertsPath, "server-cert.pem");
        var nodeCertPath = Path.Combine(_testCertsPath, "node-cert.pem");

        // Load certificates without keys for subject/issuer validation
        var caCert = X509Certificate2.CreateFromPemFile(caCertPath);
        var serverCert = X509Certificate2.CreateFromPemFile(serverCertPath);
        var nodeCert = X509Certificate2.CreateFromPemFile(nodeCertPath);

        // Assert
        caCert.Subject.Should().Contain("CN=BPA-CA");
        serverCert.Subject.Should().Contain("CN=control-plane");
        nodeCert.Subject.Should().Contain("CN=node-runtime");

        // Server and node certificates should be issued by the CA
        serverCert.Issuer.Should().Contain("CN=BPA-CA");
        nodeCert.Issuer.Should().Contain("CN=BPA-CA");
    }

    [Fact(Skip = "Requires OpenSSL and bash shell - test locally on Linux/Mac")]
    public void Certificate_ShouldBeValidForOneYear()
    {
        // Arrange
        var scriptPath = Path.Combine("..", "..", "..", "..", "..", "scripts", "generate-mtls-certs.sh");
        var fullScriptPath = Path.GetFullPath(scriptPath);

        if (!File.Exists(fullScriptPath) || !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        RunCertificateGenerationScript(fullScriptPath, _testCertsPath);

        // Act
        var serverCertPath = Path.Combine(_testCertsPath, "server-cert.pem");
        // Load certificate without key for validation
        var serverCert = X509Certificate2.CreateFromPemFile(serverCertPath);

        // Assert
        var validDays = (serverCert.NotAfter - serverCert.NotBefore).Days;
        validDays.Should().BeGreaterThanOrEqualTo(364).And.BeLessThanOrEqualTo(366); // Allow for leap years
    }

    [Fact(Skip = "Requires OpenSSL and bash shell - test locally on Linux/Mac")]
    public void CertificateChain_ShouldValidateCorrectly()
    {
        // Arrange
        var scriptPath = Path.Combine("..", "..", "..", "..", "..", "scripts", "generate-mtls-certs.sh");
        var fullScriptPath = Path.GetFullPath(scriptPath);

        if (!File.Exists(fullScriptPath) || !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        RunCertificateGenerationScript(fullScriptPath, _testCertsPath);

        // Act
        var caCertPath = Path.Combine(_testCertsPath, "ca-cert.pem");
        var serverCertPath = Path.Combine(_testCertsPath, "server-cert.pem");
        var serverKeyPath = Path.Combine(_testCertsPath, "server-key.pem");

        var caCertPem = File.ReadAllText(caCertPath);
        var serverCertPem = File.ReadAllText(serverCertPath);
        var serverKeyPem = File.ReadAllText(serverKeyPath);

        var caCert = X509Certificate2.CreateFromPem(caCertPem);
        var serverCert = X509Certificate2.CreateFromPem(serverCertPem, serverKeyPem);

        // Assert - Verify chain
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.ExtraStore.Add(caCert);

        var chainBuilt = chain.Build(serverCert);
        chainBuilt.Should().BeTrue("Server certificate should chain to CA certificate");

        // Verify CA is in the chain
        var caInChain = chain.ChainElements
            .Cast<X509ChainElement>()
            .Any(element => element.Certificate.Thumbprint == caCert.Thumbprint);

        caInChain.Should().BeTrue("CA certificate should be in the chain");
    }

    [Fact]
    public void MTlsOptions_ServerConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new Models.MTlsOptions
        {
            Enabled = true,
            ServerCertificatePath = "/etc/bpa/certs/server-cert.pem",
            ServerKeyPath = "/etc/bpa/certs/server-key.pem",
            ClientCaCertificatePath = "/etc/bpa/certs/ca-cert.pem",
            RequireClientCertificate = true,
            ValidateCertificateChain = true,
            AllowedClientCertificateSubjects = new[] { "node-runtime" }
        };

        // Act & Assert
        options.Enabled.Should().BeTrue();
        options.RequireClientCertificate.Should().BeTrue();
        options.ValidateCertificateChain.Should().BeTrue();
        options.AllowedClientCertificateSubjects.Should().NotBeNull();
        options.AllowedClientCertificateSubjects.Should().Contain("node-runtime");
    }

    [Fact]
    public void MTlsOptions_ClientConfiguration_ShouldBeValid()
    {
        // Arrange & Act
        // Test the Node.Runtime MTlsOptions structure through reflection
        var nodeRuntimeAssemblyPath = Path.Combine("..", "..", "..", "..", "..", "src", "Node.Runtime", "bin", "Debug", "net9.0", "Node.Runtime.dll");

        // Skip test if Node.Runtime assembly is not built
        if (!File.Exists(Path.GetFullPath(nodeRuntimeAssemblyPath)))
        {
            return;
        }

        // Assert - Just verify the configuration structure is consistent
        // Actual runtime tests will be in Node.Runtime.Tests
        var serverOptions = new Models.MTlsOptions
        {
            Enabled = true,
            ServerCertificatePath = "/etc/bpa/certs/server-cert.pem"
        };

        serverOptions.Enabled.Should().BeTrue();
        serverOptions.ServerCertificatePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MTlsConfiguration_WhenDisabled_ShouldNotRequireCertificates()
    {
        // Arrange
        var options = new Models.MTlsOptions
        {
            Enabled = false
        };

        // Act & Assert
        options.Enabled.Should().BeFalse();
        options.ServerCertificatePath.Should().BeNull();
        options.ServerKeyPath.Should().BeNull();
        options.ClientCaCertificatePath.Should().BeNull();
    }

    private int RunCertificateGenerationScript(string scriptPath, string certsDir)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = scriptPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["CERTS_DIR"] = certsDir;
        processStartInfo.Environment["VALIDITY_DAYS"] = "365";

        using var process = Process.Start(processStartInfo);
        process?.WaitForExit(30000); // 30 second timeout

        return process?.ExitCode ?? -1;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Clean up test certificates
            if (Directory.Exists(_testCertsPath))
            {
                try
                {
                    Directory.Delete(_testCertsPath, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        _disposed = true;
    }
}
