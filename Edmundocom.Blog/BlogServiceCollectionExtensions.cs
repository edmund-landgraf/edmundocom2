using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Edmundocom.Blog;

public static class BlogServiceCollectionExtensions
{
    public static IServiceCollection AddEdmundocomBlog(this IServiceCollection services, IConfigurationSection section)
    {
        services.Configure<BlogOptions>(section);
        services.AddSingleton<IBlogPostRepository, FileBlogPostRepository>();

        return services;
    }
}
