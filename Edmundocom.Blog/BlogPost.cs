namespace Edmundocom.Blog;

public class BlogPost
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string Summary { get; init; } = string.Empty;
    public DateOnly PublishedOn { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public bool IsDraft { get; init; }
    public required string HtmlBody { get; init; }
}
