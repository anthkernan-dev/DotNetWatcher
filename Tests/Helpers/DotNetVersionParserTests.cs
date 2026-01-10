using DotNetWatcher.Helpers;

namespace Tests.Helpers;

public class DotNetVersionParserTests
{
    private readonly DotNetVersionParser _parser = new();

    #region TryParse Tests

    [Theory]
    [InlineData(".NET 8.0", 8, 0, 0)]
    [InlineData(".NET 9.0", 9, 0, 0)]
    [InlineData(".NET 6.0.1", 6, 0, 1)]
    [InlineData("net8.0", 8, 0, 0)]
    [InlineData("NET 10.0", 10, 0, 0)]
    [InlineData(".net 7.0.5", 7, 0, 5)]
    public void TryParse_ValidVersions_ReturnsTrue(string input, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        // Act
        var success = _parser.TryParse(input, out var version);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedMajor, version.Major);
        Assert.Equal(expectedMinor, version.Minor);
        Assert.Equal(expectedPatch, version.Build);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Not a version")]
    [InlineData("Version 8.0")]
    [InlineData("Java 17")]
    public void TryParse_InvalidVersions_ReturnsFalse(string? input)
    {
        // Act
        var success = _parser.TryParse(input!, out var version);

        // Assert
        Assert.False(success);
    }

    [Theory]
    [InlineData(".NET 8", 8, 0)]
    [InlineData("net9", 9, 0)]
    public void TryParse_VersionWithoutMinor_ParsesCorrectly(string input, int expectedMajor, int expectedMinor)
    {
        // Act
        var success = _parser.TryParse(input, out var version);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedMajor, version.Major);
        Assert.Equal(expectedMinor, version.Minor);
    }

    [Fact]
    public void TryParse_CaseInsensitive()
    {
        // Arrange
        var inputs = new[] { ".NET 8.0", ".net 8.0", ".Net 8.0", "NET 8.0" };

        foreach (var input in inputs)
        {
            // Act
            var success = _parser.TryParse(input, out var version);

            // Assert
            Assert.True(success, $"Failed to parse: {input}");
            Assert.Equal(8, version.Major);
        }
    }

    #endregion

    #region SortVersions Tests

    [Fact]
    public void SortVersions_SortsDescendingByMajorVersion()
    {
        // Arrange
        var versions = new[] { ".NET 6.0", ".NET 9.0", ".NET 8.0", ".NET 7.0" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Equal(".NET 9.0", sorted[0]);
        Assert.Equal(".NET 8.0", sorted[1]);
        Assert.Equal(".NET 7.0", sorted[2]);
        Assert.Equal(".NET 6.0", sorted[3]);
    }

    [Fact]
    public void SortVersions_SortsDescendingByMinorVersion()
    {
        // Arrange
        var versions = new[] { ".NET 8.0", ".NET 8.1", ".NET 8.2" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Equal(".NET 8.2", sorted[0]);
        Assert.Equal(".NET 8.1", sorted[1]);
        Assert.Equal(".NET 8.0", sorted[2]);
    }

    [Fact]
    public void SortVersions_SortsDescendingByPatchVersion()
    {
        // Arrange
        var versions = new[] { ".NET 8.0.1", ".NET 8.0.3", ".NET 8.0.2" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Equal(".NET 8.0.3", sorted[0]);
        Assert.Equal(".NET 8.0.2", sorted[1]);
        Assert.Equal(".NET 8.0.1", sorted[2]);
    }

    [Fact]
    public void SortVersions_PlacesUnparseableVersionsLast()
    {
        // Arrange
        var versions = new[] { ".NET 8.0", "Unknown Version", ".NET 9.0", "Java 17" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Equal(".NET 9.0", sorted[0]);
        Assert.Equal(".NET 8.0", sorted[1]);
        // Unparseable versions at the end (order among themselves is alphabetical)
    }

    [Fact]
    public void SortVersions_HandlesEmptyList()
    {
        // Arrange
        var versions = Array.Empty<string>();

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Empty(sorted);
    }

    [Fact]
    public void SortVersions_HandlesMixedFormats()
    {
        // Arrange
        var versions = new[] { "net8.0", ".NET 9.0", ".net 7.0", "NET 6.0" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert
        Assert.Equal(".NET 9.0", sorted[0]);
        Assert.Equal("net8.0", sorted[1]);
        Assert.Equal(".net 7.0", sorted[2]);
        Assert.Equal("NET 6.0", sorted[3]);
    }

    [Fact]
    public void SortVersions_PreservesOriginalStringFormat()
    {
        // Arrange
        var versions = new[] { ".NET 8.0", "net9.0" };

        // Act
        var sorted = _parser.SortVersions(versions).ToList();

        // Assert - Original formats are preserved
        Assert.Contains(".NET 8.0", sorted);
        Assert.Contains("net9.0", sorted);
    }

    #endregion
}