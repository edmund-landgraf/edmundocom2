using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Edmundocom.Blog.Areas.Blog.Pages;

public class IndexModel : PageModel
{
    private readonly IBlogPostRepository _posts;
    private readonly IOptionsSnapshot<BlogOptions> _options;

    public IndexModel(IBlogPostRepository posts, IOptionsSnapshot<BlogOptions> options)
    {
        _posts = posts;
        _options = options;
    }

    public BlogOptions Options => _options.Value;
    public IReadOnlyList<BlogPostSummary> Posts { get; private set; } = Array.Empty<BlogPostSummary>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Posts = await _posts.GetPublishedPostsAsync(cancellationToken);
    }
}
