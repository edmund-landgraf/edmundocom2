using Edmundocom.Photos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Edmundocom.Pages.Admin.Photos;

public sealed class OAuthCallbackModel : PageModel
{
    private const string AccessTokenKey = "GooglePhotos.AccessToken";
    private const string PickerSessionIdKey = "GooglePhotos.PickerSessionId";
    private const string PickerUriKey = "GooglePhotos.PickerUri";
    private const string OAuthStateKey = "GooglePhotos.OAuthState";

    private readonly GooglePhotosPickerClient _pickerClient;

    public OAuthCallbackModel(GooglePhotosPickerClient pickerClient)
    {
        _pickerClient = pickerClient;
    }

    public async Task<IActionResult> OnGet(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return RedirectToPage("/Admin/Photos/Index", new { error = $"Google rejected the connection: {error}" });
        }

        var expectedState = HttpContext.Session.GetString(OAuthStateKey);

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(expectedState) ||
            !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return RedirectToPage("/Admin/Photos/Index", new { error = "Google OAuth state did not match. Please connect again." });
        }

        var callbackUrl = Url.Page(
            "/Admin/Photos/OAuthCallback",
            pageHandler: null,
            values: null,
            protocol: Request.Scheme)!;

        try
        {
            var token = await _pickerClient.ExchangeCodeAsync(code, callbackUrl, cancellationToken);
            HttpContext.Session.SetString(AccessTokenKey, token.AccessToken);

            var session = await _pickerClient.CreateSessionAsync(token.AccessToken, cancellationToken);
            HttpContext.Session.SetString(PickerSessionIdKey, session.Id);
            HttpContext.Session.SetString(PickerUriKey, AddAutoclose(session.PickerUri));
            HttpContext.Session.Remove(OAuthStateKey);

            return RedirectToPage("/Admin/Photos/Index", new { status = "Google Photos connected. Open the picker, choose media, then import it here." });
        }
        catch (HttpRequestException ex)
        {
            return RedirectToPage("/Admin/Photos/Index", new { error = $"Google Photos connection failed: {ex.Message}" });
        }
    }

    private static string AddAutoclose(string pickerUri)
    {
        if (string.IsNullOrWhiteSpace(pickerUri) || pickerUri.EndsWith("/autoclose", StringComparison.OrdinalIgnoreCase))
        {
            return pickerUri;
        }

        return $"{pickerUri.TrimEnd('/')}/autoclose";
    }
}
