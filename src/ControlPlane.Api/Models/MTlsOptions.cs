namespace ControlPlane.Api.Models;

/// <summary>
/// Configuration options for mutual TLS (mTLS) on gRPC connections.
/// </summary>
public class MTlsOptions
{
    /// <summary>
    /// Indicates whether mTLS is enabled for gRPC connections.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Path to the server certificate file (PEM format).
    /// </summary>
    public string? ServerCertificatePath { get; set; }

    /// <summary>
    /// Path to the server private key file (PEM format).
    /// </summary>
    public string? ServerKeyPath { get; set; }

    /// <summary>
    /// Path to the certificate authority (CA) certificate file for validating client certificates (PEM format).
    /// </summary>
    public string? ClientCaCertificatePath { get; set; }

    /// <summary>
    /// Indicates whether client certificate validation is required.
    /// When true, clients must present a valid certificate signed by the CA.
    /// </summary>
    public bool RequireClientCertificate { get; set; } = true;

    /// <summary>
    /// Indicates whether to validate the certificate chain.
    /// </summary>
    public bool ValidateCertificateChain { get; set; } = true;

    /// <summary>
    /// Allowed certificate subject names (Common Names) for client certificates.
    /// If empty, any certificate signed by the CA is accepted.
    /// </summary>
    public string[]? AllowedClientCertificateSubjects { get; set; }
}
