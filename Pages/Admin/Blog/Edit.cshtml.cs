using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Edmundocom.Pages.Admin.Blog;

public class EditModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsSnapshot<Edmundocom.Blog.BlogOptions> _options;

    public EditModel(IWebHostEnvironment environment, IOptionsSnapshot<Edmundocom.Blog.BlogOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    [BindProperty]
    public BlogEntryInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public bool IsNew { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? slug)
    {
        IsNew = string.IsNullOrWhiteSpace(slug);

        if (IsNew)
        {
            Input.PublishedOn = DateOnly.FromDateTime(DateTime.Today);
            return Page();
        }

        var file = GetPostPath(slug!);
        if (!System.IO.File.Exists(file))
        {
            return NotFound();
        }

        var source = await System.IO.File.ReadAllTextAsync(file);
        var (metadata, body) = BlogMarkdown.ParseFrontMatter(source);

        Input = new BlogEntryInput
        {
            Title = metadata.GetValueOrDefault("title", string.Empty),
            Slug = Path.GetFileNameWithoutExtension(file),
            Summary = metadata.GetValueOrDefault("summary", string.Empty),
            PublishedOn = DateOnly.TryParse(metadata.GetValueOrDefault("published"), out var published)
                ? published
                : DateOnly.FromDateTime(DateTime.Today),
            Tags = metadata.GetValueOrDefault("tags", string.Empty),
            IsDraft = bool.TryParse(metadata.GetValueOrDefault("draft"), out var draft) && draft,
            Body = body
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        IsNew = string.IsNullOrWhiteSpace(slug);
        Input.Slug = BlogMarkdown.Slugify(Input.Slug);

        if (string.IsNullOrWhiteSpace(Input.Slug))
        {
            ModelState.AddModelError("Input.Slug", "Slug is required.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Directory.CreateDirectory(GetContentRoot());

        var path = GetPostPath(Input.Slug);
        if (IsNew && System.IO.File.Exists(path))
        {
            ModelState.AddModelError("Input.Slug", "A post with this slug already exists.");
            return Page();
        }

        var markdown = BlogMarkdown.BuildPost(Input);
        await System.IO.File.WriteAllTextAsync(path, markdown);

        SuccessMessage = "Blog post saved.";
        return RedirectToPage("/Admin/Blog/Edit", new { slug = Input.Slug });
    }

    private string GetPostPath(string slug)
    {
        return Path.Combine(GetContentRoot(), $"{BlogMarkdown.Slugify(slug)}.md");
    }

    private string GetContentRoot()
    {
        var configuredPath = _options.Value.ContentPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    public class BlogEntryInput
    {
        [Required]
        [StringLength(140)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers, and hyphens.")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [StringLength(320)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Published on")]
        public DateOnly PublishedOn { get; set; }

        [Display(Name = "Tags")]
        public string Tags { get; set; } = string.Empty;

        [Display(Name = "Draft")]
        public bool IsDraft { get; set; }

        [Required]
        [Display(Name = "Body")]
        public string Body { get; set; } = string.Empty;
    }
}
