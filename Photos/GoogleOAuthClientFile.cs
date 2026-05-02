using System.Text.Json.Serialization;

namespace Edmundocom.Photos;

public sealed class GoogleOAuthClientFile
{
    [JsonPropertyName("web")]
    public GoogleOAuthWebClient Web { get; set; } = new();
}

public sealed class GoogleOAuthWebClient
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = "";

    [JsonPropertyName("auth_uri")]
    public string AuthUri { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";

    [JsonPropertyName("token_uri")]
    public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";
}
