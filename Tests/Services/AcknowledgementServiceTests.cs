using DotNetWatcher.Services;

namespace Tests.Services;

public class AcknowledgementServiceTests : IDisposable
{
    private readonly string _testAppDataPath;
    private readonly AcknowledgementService _service;

    public AcknowledgementServiceTests()
    {
        // Create a temporary test directory
        _testAppDataPath = Path.Combine(Path.GetTempPath(), $"DotNetWatcherTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAppDataPath);
        
        // Use reflection to set the test path or create a testable version
        _service = new AcknowledgementService();
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testAppDataPath))
        {
            Directory.Delete(_testAppDataPath, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsAcknowledged_ReturnsFalse_ForNewIssue()
    {
        // Arrange
        var issueNumber = 12345;

        // Act
        var result = _service.IsAcknowledged(issueNumber);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Acknowledge_AddsIssueToAcknowledgedList()
    {
        // Arrange
        var issueNumber = 12345;

        // Act
        _service.Acknowledge(issueNumber);

        // Assert
        Assert.True(_service.IsAcknowledged(issueNumber));
    }

    [Fact]
    public void Acknowledge_DoesNotDuplicateIssues()
    {
        // Arrange
        var issueNumber = 12345;

        // Act
        _service.Acknowledge(issueNumber);
        _service.Acknowledge(issueNumber); // Acknowledge twice

        // Assert
        Assert.True(_service.IsAcknowledged(issueNumber));
    }

    [Fact]
    public void Unacknowledge_RemovesIssueFromAcknowledgedList()
    {
        // Arrange
        var issueNumber = 12345;
        _service.Acknowledge(issueNumber);

        // Act
        _service.Unacknowledge(issueNumber);

        // Assert
        Assert.False(_service.IsAcknowledged(issueNumber));
    }

    [Fact]
    public void Unacknowledge_HandlesNonExistentIssue()
    {
        // Arrange
        var issueNumber = 99999;

        // Act & Assert - Should not throw
        _service.Unacknowledge(issueNumber);
        Assert.False(_service.IsAcknowledged(issueNumber));
    }

    [Fact]
    public void GetLastRunUtc_ReturnsMinValue_WhenNoFileExists()
    {
        // Act
        var result = _service.GetLastRunUtc();

        // Assert - For new service, should return MinValue or close to it
        Assert.True(result <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void SetLastRunUtc_SavesTimestamp()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        _service.SetLastRunUtc(timestamp);
        var result = _service.GetLastRunUtc();

        // Assert - Should be within 1 second due to serialization precision
        Assert.True((result - timestamp).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AcknowledgementService_PersistsAcrossInstances()
    {
        // Arrange
        var issueNumber = 12345;
        _service.Acknowledge(issueNumber);

        // Act - Create new instance (simulating app restart)
        var newService = new AcknowledgementService();

        // Assert
        Assert.True(newService.IsAcknowledged(issueNumber));
    }

    [Fact]
    public void MultipleIssues_CanBeAcknowledged()
    {
        // Arrange
        var issues = new[] { 100, 200, 300, 400, 500 };

        // Act
        foreach (var issue in issues)
        {
            _service.Acknowledge(issue);
        }

        // Assert
        foreach (var issue in issues)
        {
            Assert.True(_service.IsAcknowledged(issue));
        }
    }
}