using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace DotNetWatcher;

public sealed partial class MarkdownToFlowDocumentConverter : IValueConverter
{
    // VS Dark Theme Colors
    private static readonly Color VsCodeBackground = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color VsTextForeground = Color.FromRgb(0xF1, 0xF1, 0xF1);
    private static readonly Color VsTextSecondary = Color.FromRgb(0x9D, 0x9D, 0x9D);
    private static readonly Color VsHighlight = Color.FromRgb(0x00, 0x7A, 0xCC);

    // Consistent font sizes matching MainWindow.xaml
    private const double BaseFontSize = 13;
    private const double HeadingFontSize = 12;
    private const double CodeFontSize = 12;

    // Regex patterns
    [GeneratedRegex(@"<a\s+name=""[^""]+"">\s*</a>\s*", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)")]
    private static partial Regex LinkRegex();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var markdown = value as string ?? string.Empty;

        return CreateDocument(markdown);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static FlowDocument CreateDocument(string markdown)
    {
        markdown = markdown.Replace("\r\n", "\n");

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0, 0, 14, 0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(VsTextForeground),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = BaseFontSize,
            LineHeight = 20,
        };

        var lines = markdown.Split('\n');
        var inCodeFence = false;
        var codeBuffer = new System.Text.StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine;

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inCodeFence)
                {
                    inCodeFence = true;
                    codeBuffer.Clear();
                }
                else
                {
                    inCodeFence = false;
                    doc.Blocks.Add(CreateCodeBlock(codeBuffer.ToString()));
                }

                continue;
            }

            if (inCodeFence)
            {
                codeBuffer.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 4, 0, 4) });
                continue;
            }

            // Drop HTML anchors often included in headings: ## <a name="x"></a>Title or ### <a name="x"></a>Title
            line = AnchorTagRegex().Replace(line, string.Empty);

            // Handle different heading levels
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                doc.Blocks.Add(CreateHeading(line[4..].Trim()));
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                doc.Blocks.Add(CreateHeading(line[3..].Trim()));
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                // Skip top-level headings as they're usually the title which is already displayed
                continue;
            }

            doc.Blocks.Add(CreateParagraph(line.Trim()));
        }

        return doc;
    }

    private static Block CreateHeading(string text)
    {
        var p = new Paragraph
        {
            Margin = new Thickness(0, 12, 0, 4),
            FontSize = HeadingFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(VsTextSecondary),
        };

        p.Inlines.AddRange(ParseInlines(text));
        return p;
    }

    private static Block CreateCodeBlock(string code)
    {
        code = code.TrimEnd('\n');

        var p = new Paragraph
        {
            Margin = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = CodeFontSize,
            Foreground = new SolidColorBrush(VsTextForeground),
        };

        p.Inlines.Add(new Run(code));

        return new BlockUIContainer(
            new Border
            {
                Background = new SolidColorBrush(VsCodeBackground),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Child = new RichTextBox
                {
                    IsReadOnly = true,
                    IsDocumentEnabled = true,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(VsTextForeground),
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    FontSize = CodeFontSize,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                    Document = new FlowDocument(p)
                    {
                        PagePadding = new Thickness(0, 0, 14, 0),
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(VsTextForeground),
                    }
                }
            })
        { Margin = new Thickness(0, 6, 0, 6) };
    }

    private static Block CreateParagraph(string text)
    {
        var p = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 8),
            FontSize = BaseFontSize,
            Foreground = new SolidColorBrush(VsTextForeground),
        };

        foreach (var inline in ParseInlines(text))
        {
            p.Inlines.Add(inline);
        }

        return p;
    }

    private static Inline[] ParseInlines(string text)
    {
        var matches = LinkRegex().Matches(text);
        if (matches.Count == 0)
        {
            return [new Run(text)];
        }

        var inlines = new System.Collections.Generic.List<Inline>();
        var currentIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > currentIndex)
            {
                inlines.Add(new Run(text[currentIndex..match.Index]));
            }

            var label = match.Groups["label"].Value;
            var url = match.Groups["url"].Value;

            var hyperlink = new Hyperlink(new Run(label))
            {
                Foreground = new SolidColorBrush(VsHighlight),
                TextDecorations = null,
            };

            // Keep anchors as plain text links (they won't navigate in browser as-is).
            // If you want, you can map "#foo" to issue Url + "#foo" later.
            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                hyperlink.NavigateUri = absoluteUri;
            }

            inlines.Add(hyperlink);
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            inlines.Add(new Run(text[currentIndex..]));
        }

        return inlines.ToArray();
    }
}