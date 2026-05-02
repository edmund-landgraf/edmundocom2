using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Edmundocom.Pages.Admin.Blog;

public class IndexModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsSnapshot<Edmundocom.Blog.BlogOptions> _options;

    public IndexModel(IWebHostEnvironment environment, IOptionsSnapshot<Edmundocom.Blog.BlogOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    public IReadOnlyList<AdminBlogPostSummary> Posts { get; private set; } = Array.Empty<AdminBlogPostSummary>();

    public async Task OnGetAsync()
    {
        var posts = new List<AdminBlogPostSummary>();
        var root = GetContentRoot();

        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
            {
                var source = await System.IO.File.ReadAllTextAsync(file);
                var metadata = BlogMarkdown.ParseFrontMatter(source).Metadata;
                posts.Add(new AdminBlogPostSummary
                {
                    Slug = Path.GetFileNameWithoutExtension(file),
                    Title = metadata.GetValueOrDefault("title", Path.GetFileNameWithoutExtension(file)),
                    PublishedOn = DateOnly.TryParse(metadata.GetValueOrDefault("published"), out var published)
                        ? published
                        : DateOnly.FromDateTime(DateTime.Today),
                    IsDraft = bool.TryParse(metadata.GetValueOrDefault("draft"), out var draft) && draft
                });
            }
        }

        Posts = posts.OrderByDescending(post => post.PublishedOn).ThenBy(post => post.Title).ToList();
    }

    private string GetContentRoot()
    {
        var configuredPath = _options.Value.ContentPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    public class AdminBlogPostSummary
    {
        public required string Slug { get; init; }
        public required string Title { get; init; }
        public DateOnly PublishedOn { get; init; }
        public bool IsDraft { get; init; }
    }
}
