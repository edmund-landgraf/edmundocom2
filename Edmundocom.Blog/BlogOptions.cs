namespace Edmundocom.Blog;

public class BlogOptions
{
    public string Title { get; set; } = "Blog";
    public string Description { get; set; } = "Notes and updates.";
    public string ContentPath { get; set; } = "Content/Blog";
    public int RecentPostCount { get; set; } = 20;
}
