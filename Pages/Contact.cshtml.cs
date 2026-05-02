using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Edmundocom.Pages;

public class ContactModel : PageModel
{
    private const string CaptchaAnswerKey = "ContactCaptchaAnswer";
    private const string CaptchaQuestionKey = "ContactCaptchaQuestion";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ContactModel> _logger;

    public ContactModel(IConfiguration configuration, ILogger<ContactModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public ContactInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? CaptchaQuestion { get; set; }

    [TempData]
    public int CaptchaAnswer { get; set; }

    public void OnGet()
    {
        CreateCaptcha();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var expectedAnswer = TempData.Peek(CaptchaAnswerKey) as int?;

        if (!string.IsNullOrWhiteSpace(Input.Website))
        {
            _logger.LogInformation("Contact form honeypot field was populated.");
            SuccessMessage = "Thanks. Your message was sent.";
            CreateCaptcha();
            return RedirectToPage();
        }

        if (expectedAnswer is null || Input.CaptchaAnswer != expectedAnswer.Value)
        {
            ModelState.AddModelError("Input.CaptchaAnswer", "Please answer the captcha correctly.");
        }

        if (!ModelState.IsValid)
        {
            CreateCaptcha();
            return Page();
        }

        try
        {
            await SendEmailAsync();
            TempData.Remove(CaptchaAnswerKey);
            TempData.Remove(CaptchaQuestionKey);
            SuccessMessage = "Thanks. Your message was sent.";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Contact form email settings are incomplete.");
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Contact form email failed to send.");
            ModelState.AddModelError(string.Empty, "The message could not be sent right now. Please try again later.");
        }

        CreateCaptcha();
        return Page();
    }

    private void CreateCaptcha()
    {
        var left = RandomNumberGenerator.GetInt32(2, 10);
        var right = RandomNumberGenerator.GetInt32(2, 10);

        CaptchaQuestion = $"{left} + {right} = ?";
        CaptchaAnswer = left + right;
        TempData[CaptchaQuestionKey] = CaptchaQuestion;
        TempData[CaptchaAnswerKey] = CaptchaAnswer;
    }

    private async Task SendEmailAsync()
    {
        var settings = _configuration.GetSection("ContactEmail").Get<ContactEmailSettings>() ?? new ContactEmailSettings();

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            throw new InvalidOperationException("Contact email is not configured yet. Add ContactEmail:SmtpHost and SMTP credentials.");
        }

        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Contact email credentials are missing. Add ContactEmail:UserName and ContactEmail:Password.");
        }

        var recipient = string.IsNullOrWhiteSpace(settings.To)
            ? "edmund.landgraf@gmail.com"
            : settings.To;

        var from = string.IsNullOrWhiteSpace(settings.From)
            ? settings.UserName
            : settings.From;

        using var message = new MailMessage(from, recipient)
        {
            Subject = $"edmundo.com contact: {Input.Subject}",
            Body = $"""
                   Name: {Input.Name}
                   Email: {Input.Email}
                   Subject: {Input.Subject}

                   Message:
                   {Input.Message}
                   """,
            IsBodyHtml = false
        };
        message.ReplyToList.Add(new MailAddress(Input.Email, Input.Name));

        using var client = new SmtpClient(settings.SmtpHost, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = new NetworkCredential(settings.UserName, settings.Password)
        };

        await client.SendMailAsync(message);
    }

    public class ContactInput
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(160)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(140)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(4000, MinimumLength = 10)]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Website")]
        public string? Website { get; set; }

        [Required]
        [Display(Name = "Captcha answer")]
        public int? CaptchaAnswer { get; set; }
    }

    public class ContactEmailSettings
    {
        public string To { get; set; } = "edmund.landgraf@gmail.com";
        public string? From { get; set; }
        public string? SmtpHost { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }
}
