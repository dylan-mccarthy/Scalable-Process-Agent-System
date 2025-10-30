using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

public class VersionValidatorTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.1.0", true)]
    [InlineData("1.2.3", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0.0-alpha", true)]
    [InlineData("1.0.0-beta.1", true)]
    [InlineData("1.0.0-rc.1", true)]
    [InlineData("1.0.0-0.3.7", true)]
    [InlineData("1.0.0-x.7.z.92", true)]
    [InlineData("1.0.0+20130313144700", true)]
    [InlineData("1.0.0-beta+exp.sha.5114f85", true)]
    [InlineData("1.0.0+21AF26D3-117B344092BD", true)]
    [InlineData("1.0.0-alpha.1+build.123", true)]
    [InlineData("2.0.0-rc.1+build.456", true)]
    public void IsValidSemVer_WithValidVersions_ReturnsTrue(string version, bool expected)
    {
        // Act
        var result = VersionValidator.IsValidSemVer(version);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0", false)]
    [InlineData("1", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("1.0.0.0", false)]
    [InlineData("1.0.0-", false)]
    [InlineData("1.0.0+", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("abc", false)]
    [InlineData("1.0.0-alpha..1", false)]
    [InlineData("1.0.0-01", false)] // Leading zeros not allowed
    [InlineData("01.0.0", false)] // Leading zeros not allowed
    public void IsValidSemVer_WithInvalidVersions_ReturnsFalse(string version, bool expected)
    {
        // Act
        var result = VersionValidator.IsValidSemVer(version);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.2.3-alpha")]
    [InlineData("2.0.0+build.123")]
    public void ValidateOrThrow_WithValidVersion_DoesNotThrow(string version)
    {
        // Act & Assert - should not throw
        VersionValidator.ValidateOrThrow(version);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("invalid")]
    [InlineData("")]
    public void ValidateOrThrow_WithInvalidVersion_ThrowsArgumentException(string version)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => VersionValidator.ValidateOrThrow(version));
        Assert.Contains("not a valid semantic version", exception.Message);
    }

    [Fact]
    public void ValidateOrThrow_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => VersionValidator.ValidateOrThrow(null!));
    }
}
