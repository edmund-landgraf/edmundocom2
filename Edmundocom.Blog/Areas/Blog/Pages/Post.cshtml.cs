using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Edmundocom.Blog.Areas.Blog.Pages;

public class PostModel : PageModel
{
    private readonly IBlogPostRepository _posts;

    public PostModel(IBlogPostRepository posts)
    {
        _posts = posts;
    }

    public BlogPost? Post { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        Post = await _posts.GetPostAsync(slug, cancellationToken);
        return Page();
    }
}
