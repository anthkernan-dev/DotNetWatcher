using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotNetWatcher.Models;

namespace DotNetWatcher.Services
{
    public partial class GitHubAdvisoryService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfiguration _configuration;

        private const string GitHubApiUrl =
        "https://api.github.com/repos/dotnet/announcements/issues";

        public GitHubAdvisoryService(AppConfiguration configuration, HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? CreateDefaultHttpClient(configuration.GitHubToken);
            _configuration = configuration;
        }

        private static HttpClient CreateDefaultHttpClient(string? gitHubToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("DotNetWatcher", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            // Add authorization header for higher rate limits (5000/hour vs 60/hour)
            if (!string.IsNullOrEmpty(gitHubToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", gitHubToken);
            }

            return client;
        }

        public async Task<List<SecurityAdvisory>> FetchAdvisoriesAsync(CancellationToken cancellationToken = default)
        {
            var allIssues = new List<GitHubIssue>();
            var page = 1;
            const int perPage = 100;

            // Paginate through all results
            while (true)
            {
                var url = $"{GitHubApiUrl}?state=open&labels=Security&per_page={perPage}&page={page}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var issues = await response.Content.ReadFromJsonAsync<List<GitHubIssue>>(JsonOptions, cancellationToken);

                if (issues is null || issues.Count == 0)
                    break;

                allIssues.AddRange(issues);

                // If we got fewer than perPage, we've reached the end
                if (issues.Count < perPage)
                    break;

                page++;
            }

            var advisories = new List<SecurityAdvisory>();

            foreach (var issue in allIssues)
            {
                if (ExcludeIfIssueHasPSALabel(issue))
                    continue;

                var affectedVersions = ExtractVersionsFromLabels(issue.Labels);
                var cveId = ExtractCveId(issue.Title);
                var priority = CalculatePriority(affectedVersions);

                var body = issue.Body ?? string.Empty;
                var sections = ParseMarkdownSections(body);

                var mitigationFactors = sections.GetValueOrDefault("Mitigation Factors", string.Empty);
                var advisoryFaq = sections.GetValueOrDefault("Advisory FAQ", string.Empty);

                var recommendation = CalculateActionRecommendation(priority, mitigationFactors, advisoryFaq);

                advisories.Add(new SecurityAdvisory
                {
                    IssueNumber = issue.Number,
                    Title = issue.Title,
                    CveId = cveId,
                    Url = issue.HtmlUrl,
                    CreatedAt = issue.CreatedAt,
                    AffectedVersions = affectedVersions,
                    Impact = priority,
                    IsNew = false, // This can be set based on your logic

                    Body = body,
                    ExecutiveSummary = sections.GetValueOrDefault("Executive Summary", string.Empty),
                    AffectedSoftware = sections.GetValueOrDefault("Affected Software", string.Empty),
                    MitigationFactors = mitigationFactors,
                    AdvisoryFaq = advisoryFaq,

                    ActionRecommendation = recommendation,
                });
            }

            return advisories;
        }

        private static Dictionary<string, string> ParseMarkdownSections(string markdown)
        {
            markdown = markdown.Replace("\r\n", "\n");

            // Only treat "## " headings as top-level sections.
            // This keeps "###" FAQ sub-headings inside the "Advisory FAQ" content.
            var headingRegex = new Regex(@"^##\s+(.+?)\s*$", RegexOptions.Multiline);

            // Regex to strip HTML anchor tags like <a name="..."></a>
            var anchorTagRegex = new Regex(@"<a\s+name=""[^""]+"">\s*</a>\s*", RegexOptions.IgnoreCase);

            var matches = headingRegex.Matches(markdown);

            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matches.Count; i++)
            {
                var current = matches[i];
                var start = current.Index + current.Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;

                var headingText = current.Groups[1].Value.Trim();
                
                // Remove anchor tags from heading text
                headingText = anchorTagRegex.Replace(headingText, string.Empty).Trim();
                
                headingText = NormalizeSectionTitle(headingText);

                var content = markdown[start..end].Trim();

                content = CleanupMarkdownWhitespace(content);

                if (!string.IsNullOrWhiteSpace(headingText) && !sections.ContainsKey(headingText))
                {
                    sections.Add(headingText, content);
                }
            }

            return sections;
        }
        private static string CleanupMarkdownWhitespace(string content)
        {
            // Reduce excessive blank lines (3+ newlines) to just 2 newlines
            content = Regex.Replace(content, @"\n{3,}", "\n\n");

            // Remove excessive whitespace after heading markers (###, ####, etc.)
            // This reduces "### Heading\n\n\nContent" to "### Heading\n\nContent"
            content = Regex.Replace(content, @"(^#{1,6}\s+.+?)\n{2,}", "$1\n", RegexOptions.Multiline);

            return content;
        }

        private static string NormalizeSectionTitle(string headingText)
        {
            // Normalize various capitalizations and phrasings
            var normalized = headingText.ToLowerInvariant();
            
            if (normalized == "faq" || normalized == "advisory faq")
                return "Advisory FAQ";

            if (normalized == "executive summary")
                return "Executive Summary";
                
            if (normalized == "affected software")
                return "Affected Software";
                
            if (normalized == "mitigation factors")
                return "Mitigation Factors";

            return headingText;
        }

        private static bool ExcludeIfIssueHasPSALabel(GitHubIssue issue)
        {
            var hasPSAlabel = issue.Labels?.Any(l => l.Name == "PSA") == true;

            return hasPSAlabel;
        }

        private List<string> ExtractVersionsFromLabels(List<GitHubLabel>? labels)
        {
            var versions = new List<string>();

            if (labels is null)
                return versions;

            foreach (var label in labels)
            {
                if (label.Name?.StartsWith(".NET") == true)
                {
                    versions.Add(label.Name);
                }
            }

            return versions;
        }

        private string ExtractCveId(string? title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            var match = CveRegex().Match(title);
            return match.Success ? match.Value : string.Empty;
        }

        private ImpactStatus CalculatePriority(List<string> affectedVersions)
        {
            if (affectedVersions.Count == 0)
                return ImpactStatus.Potential;

            foreach (var version in affectedVersions)
            {
                if (_configuration.MonitoredVersions?.Contains(version) == true)
                {
                    return ImpactStatus.Impact;
                }
            }

            return ImpactStatus.Potential;
        }

        private static ActionRecommendation CalculateActionRecommendation(
            ImpactStatus impact,
            string mitigationFactors,
            string advisoryFaq)
        {
            if (impact != ImpactStatus.Impact)
                return ActionRecommendation.Monitor;

            // If mitigation guidance exists, give teams an explicit "Mitigate" option.
            if (!string.IsNullOrWhiteSpace(mitigationFactors))
                return ActionRecommendation.Mitigate;

            // Some advisories place mitigation/repair guidance in the FAQ.
            if (advisoryFaq.Contains("How do I fix", StringComparison.OrdinalIgnoreCase) ||
                advisoryFaq.Contains("workaround", StringComparison.OrdinalIgnoreCase) ||
                advisoryFaq.Contains("mitigation", StringComparison.OrdinalIgnoreCase))
            {
                return ActionRecommendation.Mitigate;
            }

            return ActionRecommendation.Upgrade;
        }

        [GeneratedRegex(@"CVE-\d{4}-\d+", RegexOptions.Compiled)]
        private static partial Regex CveRegex();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

    }
    
}
