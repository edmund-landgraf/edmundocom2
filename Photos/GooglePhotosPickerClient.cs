using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace Edmundocom.Photos;

public sealed class GooglePhotosPickerClient
{
    public const string Scope = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;

    public GooglePhotosPickerClient(HttpClient httpClient, IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _environment = environment;
    }

    public bool HasClientFile => File.Exists(GetClientFilePath());

    public GoogleOAuthWebClient LoadOAuthClient()
    {
        var path = GetClientFilePath();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Google OAuth client file was not found.", path);
        }

        var clientFile = JsonSerializer.Deserialize<GoogleOAuthClientFile>(File.ReadAllText(path), JsonOptions);
        return clientFile?.Web ?? throw new InvalidOperationException("google_oauth_client.json does not contain a web OAuth client.");
    }

    public string BuildAuthorizationUrl(string redirectUri, string state)
    {
        var client = LoadOAuthClient();
        var authUri = string.IsNullOrWhiteSpace(client.AuthUri)
            ? "https://accounts.google.com/o/oauth2/v2/auth"
            : client.AuthUri;

        return QueryHelpers.AddQueryString(authUri, new Dictionary<string, string?>
        {
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        });
    }

    public async Task<OAuthTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var client = LoadOAuthClient();
        var tokenUri = string.IsNullOrWhiteSpace(client.TokenUri)
            ? "https://oauth2.googleapis.com/token"
            : client.TokenUri;

        using var response = await _httpClient.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = client.ClientId,
            ["client_secret"] = client.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }), cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Google OAuth returned an empty token response.");
    }

    public async Task<PickerSession> CreateSessionAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreatePickerRequest(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions", accessToken);
        request.Content = new StringContent("""{"pickingConfig":{"maxItemCount":"200"}}""", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<PickerSession>(json, JsonOptions)
            ?? throw new InvalidOperationException("Google Photos returned an empty picker session.");
    }

    public async Task<PickerSession> GetSessionAsync(string accessToken, string sessionId, CancellationToken cancellationToken)
    {
        using var request = CreatePickerRequest(HttpMethod.Get, $"https://photospicker.googleapis.com/v1/sessions/{Uri.EscapeDataString(sessionId)}", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<PickerSession>(json, JsonOptions)
            ?? throw new InvalidOperationException("Google Photos returned an empty picker session.");
    }

    public async Task<IReadOnlyList<SharedMediaItem>> ListPickedMediaAsync(string accessToken, string sessionId, CancellationToken cancellationToken)
    {
        var allItems = new List<SharedMediaItem>();
        string? pageToken = null;

        do
        {
            var url = QueryHelpers.AddQueryString("https://photospicker.googleapis.com/v1/mediaItems", new Dictionary<string, string?>
            {
                ["sessionId"] = sessionId,
                ["pageSize"] = "100",
                ["pageToken"] = pageToken
            });

            using var request = CreatePickerRequest(HttpMethod.Get, url, accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var page = JsonSerializer.Deserialize<PickedMediaResponse>(json, JsonOptions)
                ?? new PickedMediaResponse();

            allItems.AddRange(page.MediaItems.Select(ToSharedMediaItem));
            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return allItems;
    }

    public async Task DownloadMediaFilesAsync(string accessToken, IEnumerable<SharedMediaItem> items, CancellationToken cancellationToken)
    {
        var mediaRoot = Path.Combine(_environment.WebRootPath, "media", "shared");
        Directory.CreateDirectory(mediaRoot);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.BaseUrl))
            {
                continue;
            }

            var extension = GetFileExtension(item);
            var fileName = $"{SanitizeFileName(item.Id)}{extension}";
            var destinationPath = Path.Combine(mediaRoot, fileName);
            var downloadUrl = item.IsVideo ? $"{item.BaseUrl}=dv" : $"{item.BaseUrl}=w2048-h1536";

            using var request = CreatePickerRequest(HttpMethod.Get, downloadUrl, accessToken);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);

            item.LocalPath = $"/media/shared/{fileName}";
            item.BaseUrl = "";
        }
    }

    public async Task DeleteSessionAsync(string accessToken, string sessionId, CancellationToken cancellationToken)
    {
        using var request = CreatePickerRequest(HttpMethod.Delete, $"https://photospicker.googleapis.com/v1/sessions/{Uri.EscapeDataString(sessionId)}", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreatePickerRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static SharedMediaItem ToSharedMediaItem(PickedMediaItem item)
    {
        var metadata = item.MediaFile?.MediaFileMetadata;
        var filename = item.MediaFile?.Filename ?? "";

        return new SharedMediaItem
        {
            Id = item.Id,
            Title = Path.GetFileNameWithoutExtension(filename),
            Type = item.Type,
            BaseUrl = item.MediaFile?.BaseUrl ?? "",
            Filename = filename,
            MimeType = item.MediaFile?.MimeType ?? "",
            Width = metadata?.Width,
            Height = metadata?.Height,
            CreateTime = item.CreateTime,
            PickedAt = DateTimeOffset.UtcNow
        };
    }

    private string GetClientFilePath() => Path.Combine(_environment.ContentRootPath, "google_oauth_client.json");

    private static string GetFileExtension(SharedMediaItem item)
    {
        var extension = Path.GetExtension(item.Filename);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return item.MimeType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "video/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            _ when item.IsVideo => ".mp4",
            _ => ".jpg"
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }
}

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}

public sealed class PickerSession
{
    public string Id { get; set; } = "";
    public string PickerUri { get; set; } = "";
    public bool MediaItemsSet { get; set; }
    public DateTimeOffset? ExpireTime { get; set; }
    public PollingConfig? PollingConfig { get; set; }
}

public sealed class PollingConfig
{
    public string PollInterval { get; set; } = "";
    public string TimeoutIn { get; set; } = "";
}

public sealed class PickedMediaResponse
{
    public List<PickedMediaItem> MediaItems { get; set; } = [];
    public string? NextPageToken { get; set; }
}

public sealed class PickedMediaItem
{
    public string Id { get; set; } = "";
    public DateTimeOffset? CreateTime { get; set; }
    public string Type { get; set; } = "";
    public PickedMediaFile? MediaFile { get; set; }
}

public sealed class PickedMediaFile
{
    public string BaseUrl { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string Filename { get; set; } = "";
    public PickedMediaFileMetadata? MediaFileMetadata { get; set; }
}

public sealed class PickedMediaFileMetadata
{
    public int? Width { get; set; }
    public int? Height { get; set; }
}
