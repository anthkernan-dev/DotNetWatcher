using DotNetWatcher.Models;

namespace Tests.Models;

public class SecurityAdvisoryTests
{
    [Fact]
    public void CveDisplay_ReturnsCveId_WhenPresent()
    {
        // Arrange
        var advisory = new SecurityAdvisory { CveId = "CVE-2024-12345" };

        // Act
        var result = advisory.CveDisplay;

        // Assert
        Assert.Equal("CVE-2024-12345", result);
    }

    [Fact]
    public void DisplayNetUpdatesTitle()
    {
        // Arrange
        var advisory = new SecurityAdvisory
        {
            CveId = string.Empty,
            Title = "Some random string that contains the word Updates"
        };

        // Act
        var result = advisory.CveDisplay;

        // Assert
        Assert.Equal("Monthly .NET Update", result);
    }

    [Fact]
    public void HasCve_ReturnsTrue_WhenCveIdPresent()
    {
        // Arrange
        var advisory = new SecurityAdvisory { CveId = "CVE-2024-12345" };

        // Act
        var result = advisory.HasCve;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasCve_ReturnsFalse_WhenCveIdEmpty()
    {
        // Arrange
        var advisory = new SecurityAdvisory { CveId = string.Empty };

        // Act
        var result = advisory.HasCve;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MsrcUri_GeneratesCorrectUri_WhenCveIdPresent()
    {
        // Arrange
        var advisory = new SecurityAdvisory { CveId = "CVE-2024-12345" };

        // Act
        var result = advisory.MsrcUri.ToString();

        // Assert
        Assert.Equal("https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-12345", result);
    }

    [Fact]
    public void ActionRecommendationDisplay_ReturnsCorrectText()
    {
        // Arrange
        var advisories = new[]
        {
            new SecurityAdvisory { ActionRecommendation = ActionRecommendation.Monitor },
            new SecurityAdvisory { ActionRecommendation = ActionRecommendation.Mitigate },
            new SecurityAdvisory { ActionRecommendation = ActionRecommendation.Upgrade }
        };

        // Act & Assert
        Assert.Equal("Monitor", advisories[0].ActionRecommendationDisplay);
        Assert.Equal("Mitigate", advisories[1].ActionRecommendationDisplay);
        Assert.Equal("Upgrade", advisories[2].ActionRecommendationDisplay);
    }

    [Fact]
    public void PropertyChanged_RaisesEvent_WhenIsAcknowledgedChanges()
    {
        // Arrange
        var advisory = new SecurityAdvisory();
        var eventRaised = false;
        advisory.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SecurityAdvisory.IsAcknowledged))
                eventRaised = true;
        };

        // Act
        advisory.IsAcknowledged = true;

        // Assert
        Assert.True(eventRaised);
    }
}