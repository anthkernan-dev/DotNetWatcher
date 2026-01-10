using System.Net;
using System.Text;
using System.Text.Json;
using DotNetWatcher.Models;
using DotNetWatcher.Services;
using Moq;
using Moq.Protected;

namespace Tests.Services;

public class GitHubAdvisoryServiceTests
{
    private readonly AppConfiguration _testConfig;

    public GitHubAdvisoryServiceTests()
    {
        _testConfig = new AppConfiguration
        {
            MonitoredVersions = [".NET 8.0", ".NET 9.0"],
            GitHubToken = "test-token"
        };
    }

    #region FetchAdvisoriesAsync Tests

    [Fact]
    public async Task FetchAdvisoriesAsync_ReturnsEmptyList_WhenNoIssues()
    {
        // Arrange
        var httpClient = CreateMockHttpClient([]);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchAdvisoriesAsync_ReturnsParsedAdvisories_WhenIssuesExist()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 12345,
                Title = "CVE-2024-12345 - Test Vulnerability",
                Body = "## Executive Summary\nTest summary\n\n## Affected Software\n.NET 8.0",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/12345",
                CreatedAt = DateTime.UtcNow,
                Labels =
                [
                    new GitHubLabel { Name = ".NET 8.0" },
                    new GitHubLabel { Name = "Security" }
                ]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Single(result);
        var advisory = result[0];
        Assert.Equal(12345, advisory.IssueNumber);
        Assert.Equal("CVE-2024-12345 - Test Vulnerability", advisory.Title);
        Assert.Equal("CVE-2024-12345", advisory.CveId);
        Assert.Contains(".NET 8.0", advisory.AffectedVersions);
        Assert.Equal(ImpactStatus.Impact, advisory.Impact);
    }

    [Fact]
    public async Task FetchAdvisoriesAsync_ExcludesPSALabeledIssues()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Security Advisory",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            },
            new()
            {
                Number = 2,
                Title = "PSA: Public Service Announcement",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/2",
                CreatedAt = DateTime.UtcNow,
                Labels =
                [
                    new GitHubLabel { Name = "Security" },
                    new GitHubLabel { Name = "PSA" }
                ]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].IssueNumber);
    }

    [Fact]
    public async Task FetchAdvisoriesAsync_HandlesPagination()
    {
        // Arrange
        var page1Issues = Enumerable.Range(1, 100)
            .Select(i => new GitHubIssue
            {
                Number = i,
                Title = $"Issue {i}",
                HtmlUrl = $"https://github.com/dotnet/announcements/issues/{i}",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            })
            .ToList();

        var page2Issues = Enumerable.Range(101, 50)
            .Select(i => new GitHubIssue
            {
                Number = i,
                Title = $"Issue {i}",
                HtmlUrl = $"https://github.com/dotnet/announcements/issues/{i}",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            })
            .ToList();

        var httpClient = CreateMockHttpClientWithPagination(page1Issues, page2Issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(150, result.Count);
    }

    [Fact]
    public async Task FetchAdvisoriesAsync_ParsesAllMarkdownSections()
    {
        // Arrange
        var markdown = """
            ## Executive Summary
            This is the executive summary.

            ## Affected Software
            .NET 8.0 and .NET 9.0

            ## Mitigation Factors
            Apply the latest patches.

            ## Advisory FAQ

            ### How do I know if I am affected?
            Check your version.

            ### How do I fix the issue?
            Update to the latest version.
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "CVE-2024-00001",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        var advisory = result[0];
        Assert.Contains("executive summary", advisory.ExecutiveSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".NET 8.0 and .NET 9.0", advisory.AffectedSoftware);
        Assert.Contains("Apply the latest patches", advisory.MitigationFactors);
        Assert.Contains("How do I know if I am affected?", advisory.AdvisoryFaq);
    }

    #endregion

    #region ExtractCveId Tests

    [Theory]
    [InlineData("CVE-2024-12345 - Test Vulnerability", "CVE-2024-12345")]
    [InlineData("Security Advisory CVE-2023-99999", "CVE-2023-99999")]
    [InlineData("CVE-2020-1234", "CVE-2020-1234")]
    [InlineData("No CVE here", "")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public async Task ExtractCveId_ReturnsCorrectValue(string? title, string expectedCve)
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = title,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        if (!string.IsNullOrEmpty(title))
        {
            Assert.Equal(expectedCve, result[0].CveId);
        }
    }

    #endregion

    #region ExtractVersionsFromLabels Tests

    [Fact]
    public async Task ExtractVersionsFromLabels_ExtractsAllDotNetVersions()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels =
                [
                    new GitHubLabel { Name = ".NET 6.0" },
                    new GitHubLabel { Name = ".NET 8.0" },
                    new GitHubLabel { Name = ".NET 9.0" },
                    new GitHubLabel { Name = "Security" },
                    new GitHubLabel { Name = "High-Priority" }
                ]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        var versions = result[0].AffectedVersions;
        Assert.Equal(3, versions.Count);
        Assert.Contains(".NET 6.0", versions);
        Assert.Contains(".NET 8.0", versions);
        Assert.Contains(".NET 9.0", versions);
    }

    [Fact]
    public async Task ExtractVersionsFromLabels_ReturnsEmpty_WhenNoLabels()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = null
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Empty(result[0].AffectedVersions);
    }

    #endregion

    #region CalculatePriority Tests

    [Fact]
    public async Task CalculatePriority_ReturnsImpact_WhenMonitoredVersionAffected()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 8.0" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ImpactStatus.Impact, result[0].Impact);
    }

    [Fact]
    public async Task CalculatePriority_ReturnsPotential_WhenMonitoredVersionNotAffected()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 6.0" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ImpactStatus.Potential, result[0].Impact);
    }

    [Fact]
    public async Task CalculatePriority_ReturnsPotential_WhenNoVersions()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = null
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ImpactStatus.Potential, result[0].Impact);
    }

    #endregion

    #region CalculateActionRecommendation Tests

    [Fact]
    public async Task ActionRecommendation_ReturnsMonitor_WhenNotImpact()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 6.0" }] // Not monitored
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ActionRecommendation.Monitor, result[0].ActionRecommendation);
    }

    [Fact]
    public async Task ActionRecommendation_ReturnsMitigate_WhenMitigationFactorsExist()
    {
        // Arrange
        var markdown = """
            ## Mitigation Factors
            You can mitigate this by disabling feature X.
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 8.0" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ActionRecommendation.Mitigate, result[0].ActionRecommendation);
    }

    [Theory]
    [InlineData("### How do I fix the issue?\nUpdate to latest version.")]
    [InlineData("### Is there a workaround?\nYes, disable the feature.")]
    [InlineData("### Mitigation steps\nFollow these steps.")]
    public async Task ActionRecommendation_ReturnsMitigate_WhenFAQContainsMitigationInfo(string faqContent)
    {
        // Arrange
        var markdown = $"""
            ## Advisory FAQ
            {faqContent}
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 8.0" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ActionRecommendation.Mitigate, result[0].ActionRecommendation);
    }

    [Fact]
    public async Task ActionRecommendation_ReturnsUpgrade_WhenNoMitigationAvailable()
    {
        // Arrange
        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = ".NET 8.0" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        Assert.Equal(ActionRecommendation.Upgrade, result[0].ActionRecommendation);
    }

    #endregion

    #region ParseMarkdownSections Tests

    [Fact]
    public async Task ParseMarkdownSections_NormalizesSectionTitles()
    {
        // Arrange
        var markdown = """
            ## executive summary
            Content 1

            ## AFFECTED SOFTWARE
            Content 2

            ## Mitigation FACTORS
            Content 3

            ## faq
            Content 4
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        var advisory = result[0];
        Assert.Contains("Content 1", advisory.ExecutiveSummary);
        Assert.Contains("Content 2", advisory.AffectedSoftware);
        Assert.Contains("Content 3", advisory.MitigationFactors);
        Assert.Contains("Content 4", advisory.AdvisoryFaq);
    }

    [Fact]
    public async Task ParseMarkdownSections_RemovesHTMLAnchors()
    {
        // Arrange
        var markdown = """
            ## <a name="exec-summary"></a>Executive Summary
            Content here
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        var advisory = result[0];
        Assert.Contains("Content here", advisory.ExecutiveSummary);
        Assert.DoesNotContain("<a name=", advisory.ExecutiveSummary);
    }

    [Fact]
    public async Task ParseMarkdownSections_CleansUpWhitespace()
    {
        // Arrange
        var markdown = """
            ## Advisory FAQ

            ### Question 1


            Answer with too much space

            ### Question 2



            Another answer
            """;

        var issues = new List<GitHubIssue>
        {
            new()
            {
                Number = 1,
                Title = "Test",
                Body = markdown,
                HtmlUrl = "https://github.com/dotnet/announcements/issues/1",
                CreatedAt = DateTime.UtcNow,
                Labels = [new GitHubLabel { Name = "Security" }]
            }
        };

        var httpClient = CreateMockHttpClient(issues);
        var service = new GitHubAdvisoryService(_testConfig, httpClient);

        // Act
        var result = await service.FetchAdvisoriesAsync();

        // Assert
        var faq = result[0].AdvisoryFaq;
        // Should reduce excessive newlines
        Assert.DoesNotContain("\n\n\n", faq);
    }

    #endregion

    #region Helper Methods

    private static HttpClient CreateMockHttpClient(List<GitHubIssue> issues)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        var json = JsonSerializer.Serialize(issues, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        return new HttpClient(mockHandler.Object);
    }

    private static HttpClient CreateMockHttpClientWithPagination(
        List<GitHubIssue> page1Issues,
        List<GitHubIssue> page2Issues)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("page=1")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(page1Issues, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    }),
                    Encoding.UTF8,
                    "application/json")
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("page=2")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(page2Issues, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    }),
                    Encoding.UTF8,
                    "application/json")
            });

        return new HttpClient(mockHandler.Object);
    }

    #endregion
}