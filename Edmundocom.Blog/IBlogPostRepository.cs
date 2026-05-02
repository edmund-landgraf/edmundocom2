namespace Edmundocom.Blog;

public interface IBlogPostRepository
{
    Task<IReadOnlyList<BlogPostSummary>> GetPublishedPostsAsync(CancellationToken cancellationToken = default);
    Task<BlogPost?> GetPostAsync(string slug, CancellationToken cancellationToken = default);
}
