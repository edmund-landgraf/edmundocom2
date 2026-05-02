using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Edmundocom.Blog;

public partial class FileBlogPostRepository : IBlogPostRepository
{
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<BlogOptions> _options;

    public FileBlogPostRepository(IWebHostEnvironment environment, IOptionsMonitor<BlogOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    public async Task<IReadOnlyList<BlogPostSummary>> GetPublishedPostsAsync(CancellationToken cancellationToken = default)
    {
        var posts = new List<BlogPostSummary>();

        foreach (var file in GetPostFiles())
        {
            var post = await ReadPostAsync(file, cancellationToken);
            if (post is null || post.IsDraft)
            {
                continue;
            }

            posts.Add(new BlogPostSummary
            {
                Slug = post.Slug,
                Title = post.Title,
                Summary = post.Summary,
                PublishedOn = post.PublishedOn,
                Tags = post.Tags
            });
        }

        return posts
            .OrderByDescending(post => post.PublishedOn)
            .Take(Math.Max(1, _options.CurrentValue.RecentPostCount))
            .ToList();
    }

    public async Task<BlogPost?> GetPostAsync(string slug, CancellationToken cancellationToken = default)
    {
        var safeSlug = Path.GetFileNameWithoutExtension(slug);
        var file = Path.Combine(GetContentRoot(), $"{safeSlug}.md");

        if (!File.Exists(file))
        {
            return null;
        }

        var post = await ReadPostAsync(file, cancellationToken);
        return post is { IsDraft: false } ? post : null;
    }

    private IEnumerable<string> GetPostFiles()
    {
        var root = GetContentRoot();
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            : Enumerable.Empty<string>();
    }

    private string GetContentRoot()
    {
        var configuredPath = _options.CurrentValue.ContentPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    private static async Task<BlogPost?> ReadPostAsync(string file, CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(file, cancellationToken);
        var (metadata, markdown) = ParseFrontMatter(source);
        var slug = Path.GetFileNameWithoutExtension(file);

        if (!metadata.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new BlogPost
        {
            Slug = slug,
            Title = title,
            Summary = metadata.GetValueOrDefault("summary", string.Empty),
            PublishedOn = ParseDate(metadata.GetValueOrDefault("published")),
            Tags = ParseTags(metadata.GetValueOrDefault("tags")),
            IsDraft = bool.TryParse(metadata.GetValueOrDefault("draft"), out var draft) && draft,
            HtmlBody = RenderMarkdown(markdown)
        };
    }

    private static (Dictionary<string, string> Metadata, string Markdown) ParseFrontMatter(string source)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = source.Replace("\r\n", "\n");

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (metadata, normalized);
        }

        var end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return (metadata, normalized);
        }

        var header = normalized[4..end];
        foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            metadata[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
        }

        return (metadata, normalized[(end + 5)..].Trim());
    }

    private static DateOnly ParseDate(string? value)
    {
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static IReadOnlyList<string> ParseTags(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RenderMarkdown(string markdown)
    {
        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();
        var inList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                CloseList();
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph();
                CloseList();
                html.Append("<h3>").Append(FormatInline(line[4..])).AppendLine("</h3>");
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph();
                CloseList();
                html.Append("<h2>").Append(FormatInline(line[3..])).AppendLine("</h2>");
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph();
                CloseList();
                html.Append("<h1>").Append(FormatInline(line[2..])).AppendLine("</h1>");
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (!inList)
                {
                    html.AppendLine("<ul>");
                    inList = true;
                }

                html.Append("<li>").Append(FormatInline(line[2..])).AppendLine("</li>");
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append(' ');
            }

            paragraph.Append(line.Trim());
        }

        FlushParagraph();
        CloseList();

        return html.ToString();

        void FlushParagraph()
        {
            if (paragraph.Length == 0)
            {
                return;
            }

            html.Append("<p>").Append(FormatInline(paragraph.ToString())).AppendLine("</p>");
            paragraph.Clear();
        }

        void CloseList()
        {
            if (!inList)
            {
                return;
            }

            html.AppendLine("</ul>");
            inList = false;
        }
    }

    private static string FormatInline(string value)
    {
        var encoded = HtmlEncoder.Default.Encode(value);
        encoded = MarkdownLinkRegex().Replace(encoded, "<a href=\"$2\">$1</a>");
        encoded = StrongRegex().Replace(encoded, "<strong>$1</strong>");
        encoded = EmphasisRegex().Replace(encoded, "<em>$1</em>");

        return encoded;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex StrongRegex();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex EmphasisRegex();
}
