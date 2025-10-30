using System.Text.RegularExpressions;

namespace ControlPlane.Api.Services;

/// <summary>
/// Validates agent version strings against semantic versioning format.
/// </summary>
public static partial class VersionValidator
{
    // Semantic versioning regex pattern: MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
    // Examples: 1.0.0, 1.2.3-alpha, 1.2.3-beta.1, 1.0.0+build.123
    private const string SemVerPattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";

#if NET7_0_OR_GREATER
    [GeneratedRegex(SemVerPattern)]
    private static partial Regex SemVerRegex();
#else
    private static Regex SemVerRegex() => new(SemVerPattern, RegexOptions.Compiled);
#endif

    /// <summary>
    /// Validates whether a version string follows semantic versioning format.
    /// </summary>
    /// <param name="version">The version string to validate.</param>
    /// <returns>True if the version is valid, false otherwise.</returns>
    public static bool IsValidSemVer(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return SemVerRegex().IsMatch(version);
    }

    /// <summary>
    /// Validates and throws an exception if the version is invalid.
    /// </summary>
    /// <param name="version">The version string to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the version is not valid semantic versioning format.</exception>
    public static void ValidateOrThrow(string version)
    {
        if (!IsValidSemVer(version))
        {
            throw new ArgumentException(
                $"Version '{version}' is not a valid semantic version. " +
                "Expected format: MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD] (e.g., 1.0.0, 1.2.3-alpha, 2.0.0-beta.1)",
                nameof(version));
        }
    }
}
