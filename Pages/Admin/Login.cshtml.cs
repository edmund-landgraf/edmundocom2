using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Edmundocom.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LoginModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var expectedUserName = _configuration["Admin:UserName"];
        var expectedPassword = _configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(expectedUserName) ||
            string.IsNullOrWhiteSpace(expectedPassword) ||
            !string.Equals(Input.UserName, expectedUserName, StringComparison.Ordinal) ||
            !string.Equals(Input.Password, expectedPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Invalid admin login.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Input.UserName),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/Admin" : ReturnUrl);
    }

    public class LoginInput
    {
        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
