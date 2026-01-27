using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetWatcher.Models
{
    public record GitHubIssue
    {
        public int Number { get; init; }
        public string? Title { get; init; }
        public string? HtmlUrl { get; init; }
        public DateTime CreatedAt { get; init; }
        public List<GitHubLabel>? Labels { get; init; }

        public string? Body { get; init; }
    }

    public record GitHubLabel
    {
        public string? Name { get; init; }
    }
}
