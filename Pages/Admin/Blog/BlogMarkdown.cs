using System.Text;
using System.Text.RegularExpressions;

namespace Edmundocom.Pages.Admin.Blog;

public static partial class BlogMarkdown
{
    public static (Dictionary<string, string> Metadata, string Body) ParseFrontMatter(string source)
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

    public static string BuildPost(EditModel.BlogEntryInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"title: {Escape(input.Title)}");
        builder.AppendLine($"summary: {Escape(input.Summary)}");
        builder.AppendLine($"published: {input.PublishedOn:yyyy-MM-dd}");
        builder.AppendLine($"tags: {Escape(input.Tags)}");
        if (input.IsDraft)
        {
            builder.AppendLine("draft: true");
        }

        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(input.Body.Trim());

        return builder.ToString();
    }

    public static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        lower = NonSlugCharactersRegex().Replace(lower, "-");
        lower = DuplicateHyphensRegex().Replace(lower, "-");
        return lower.Trim('-');
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharactersRegex();

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateHyphensRegex();
}
