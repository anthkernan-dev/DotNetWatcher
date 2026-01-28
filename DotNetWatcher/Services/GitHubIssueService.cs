using DotNetWatcher.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetWatcher.Services;

public class GitHubIssueService
{
    private readonly HttpClient _httpClient;
    private readonly string _repoOwner;
    private readonly string _repoName;

    public GitHubIssueService(string gitHubToken, string repoOwner, string repoName)
    {
        _repoOwner = repoOwner;
        _repoName = repoName;
        _httpClient = CreateHttpClient(gitHubToken);
    }

    private static HttpClient CreateHttpClient(string gitHubToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DotNetWatcher", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        

            var token = ""; //Can't create an issue and assign to copilot via the REST endpoint with a fine grained token
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
 
        
        return client;
    }

    public async Task<CreateIssueResult> CreateRemediationIssueAsync(
        SecurityAdvisory advisory, 
        CancellationToken cancellationToken = default)
    {
        var apiUrl = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/issues";
        
        var affectedVersions = string.Join(", ", advisory.AffectedVersions);
        
        var issueBody = $"""
            ## Security Advisory Remediation

            **CVE:** {advisory.CveId}
            **Advisory:** {advisory.Title}
            **Affected Versions:** {affectedVersions}
            **Published:** {advisory.CreatedAt:MMMM dd, yyyy}

            ### Reference
            {advisory.Url}

            ### Tasks
            - [ ] Review advisory details
            - [ ] Identify affected projects/packages
            - [ ] Update to patched version
            - [ ] Test changes
            - [ ] Deploy to production

            ### Notes
            _Add any additional context or notes here._
            """;

        var request = new CreateIssueRequest
        {
            Title = $"Security: Remediate {advisory.CveId}",
            Body = issueBody,
            Assignees = ["copilot"]
        };

        var response = await _httpClient.PostAsJsonAsync(apiUrl, request, JsonOptions, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var createdIssue = await response.Content.ReadFromJsonAsync<CreateIssueResponse>(JsonOptions, cancellationToken);
            return new CreateIssueResult
            {
                Success = true,
                IssueNumber = createdIssue?.Number ?? 0,
                IssueUrl = createdIssue?.HtmlUrl ?? string.Empty
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new CreateIssueResult
        {
            Success = false,
            ErrorMessage = $"Failed to create issue: {response.StatusCode} - {errorContent}"
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public record CreateIssueRequest
{
    public required string Title { get; init; }
    public string? Body { get; init; }
    public List<string>? Assignees { get; init; }
    public List<string>? Labels { get; init; }
}

public record CreateIssueResponse
{
    public int Number { get; init; }
    public string? HtmlUrl { get; init; }
}

public record CreateIssueResult
{
    public bool Success { get; init; }
    public int IssueNumber { get; init; }
    public string IssueUrl { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}