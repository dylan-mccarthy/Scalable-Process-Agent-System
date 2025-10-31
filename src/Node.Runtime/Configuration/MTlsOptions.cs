namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for mutual TLS (mTLS) on gRPC connections.
/// </summary>
public sealed class MTlsOptions
{
    /// <summary>
    /// Indicates whether mTLS is enabled for gRPC connections.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Path to the client certificate file (PEM format).
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Path to the client private key file (PEM format).
    /// </summary>
    public string? ClientKeyPath { get; set; }

    /// <summary>
    /// Path to the certificate authority (CA) certificate file for validating the server certificate (PEM format).
    /// </summary>
    public string? ServerCaCertificatePath { get; set; }

    /// <summary>
    /// Expected server certificate subject name (Common Name).
    /// If set, the server certificate CN must match this value.
    /// </summary>
    public string? ExpectedServerCertificateSubject { get; set; }

    /// <summary>
    /// Indicates whether to validate the certificate chain.
    /// </summary>
    public bool ValidateCertificateChain { get; set; } = true;
}
