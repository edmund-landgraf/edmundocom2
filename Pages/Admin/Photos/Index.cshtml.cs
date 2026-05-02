using Edmundocom.Photos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Edmundocom.Pages.Admin.Photos;

public sealed class IndexModel : PageModel
{
    private const string AccessTokenKey = "GooglePhotos.AccessToken";
    private const string PickerSessionIdKey = "GooglePhotos.PickerSessionId";
    private const string PickerUriKey = "GooglePhotos.PickerUri";
    private const string OAuthStateKey = "GooglePhotos.OAuthState";

    private readonly SharedMediaRepository _repository;
    private readonly GooglePhotosPickerClient _pickerClient;

    public IndexModel(SharedMediaRepository repository, GooglePhotosPickerClient pickerClient)
    {
        _repository = repository;
        _pickerClient = pickerClient;
    }

    public IReadOnlyList<SharedMediaItem> Media { get; private set; } = [];
    public string? PickerUri { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet(string? status = null, string? error = null)
    {
        LoadPage(status, error);
    }

    public IActionResult OnPostConnect()
    {
        if (!_pickerClient.HasClientFile)
        {
            return RedirectToPage(new { error = "google_oauth_client.json was not found at the site root." });
        }

        var state = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString(OAuthStateKey, state);

        var callbackUrl = Url.Page(
            "/Admin/Photos/OAuthCallback",
            pageHandler: null,
            values: null,
            protocol: Request.Scheme)!;

        return Redirect(_pickerClient.BuildAuthorizationUrl(callbackUrl, state));
    }

    public async Task<IActionResult> OnPostImport(CancellationToken cancellationToken)
    {
        var accessToken = HttpContext.Session.GetString(AccessTokenKey);
        var sessionId = HttpContext.Session.GetString(PickerSessionIdKey);

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(sessionId))
        {
            return RedirectToPage(new { error = "Connect Google Photos and open a picker session first." });
        }

        try
        {
            var session = await _pickerClient.GetSessionAsync(accessToken, sessionId, cancellationToken);

            if (!session.MediaItemsSet)
            {
                return RedirectToPage(new { error = "Google Photos says that picker session is not finished yet. Finish the picker, then import again." });
            }

            var pickedItems = await _pickerClient.ListPickedMediaAsync(accessToken, sessionId, cancellationToken);
            await _pickerClient.DownloadMediaFilesAsync(accessToken, pickedItems, cancellationToken);
            _repository.Upsert(pickedItems);
            await _pickerClient.DeleteSessionAsync(accessToken, sessionId, cancellationToken);

            HttpContext.Session.Remove(PickerSessionIdKey);
            HttpContext.Session.Remove(PickerUriKey);

            return RedirectToPage(new { status = $"Imported {pickedItems.Count} media item(s)." });
        }
        catch (HttpRequestException ex)
        {
            return RedirectToPage(new { error = $"Google Photos import failed: {ex.Message}" });
        }
    }

    public IActionResult OnPostRemove(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            _repository.Remove(id);
        }

        return RedirectToPage(new { status = "Removed the media item from the public page." });
    }

    private void LoadPage(string? status, string? error)
    {
        Media = _repository.GetAll();
        PickerUri = HttpContext.Session.GetString(PickerUriKey);
        StatusMessage = status;
        ErrorMessage = error;
    }
}
