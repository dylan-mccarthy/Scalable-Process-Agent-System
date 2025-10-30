namespace ControlPlane.Api.Models;

/// <summary>
/// Configuration options for authentication.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Indicates whether authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Authentication provider type (Keycloak, EntraId).
    /// </summary>
    public string Provider { get; set; } = "Keycloak";

    /// <summary>
    /// The OIDC authority URL.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The audience for token validation.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether HTTPS metadata is required.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// The metadata address for token validation.
    /// </summary>
    public string? MetadataAddress { get; set; }

    /// <summary>
    /// Indicates whether to validate the issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Valid issuers for token validation.
    /// </summary>
    public string[]? ValidIssuers { get; set; }

    /// <summary>
    /// Indicates whether to validate the audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Valid audiences for token validation.
    /// </summary>
    public string[]? ValidAudiences { get; set; }
}
