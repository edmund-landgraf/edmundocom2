namespace Edmundocom.Photos;

public sealed class SharedMediaItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "PHOTO";
    public string BaseUrl { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string ProductUrl { get; set; } = "";
    public string Filename { get; set; } = "";
    public string MimeType { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset? CreateTime { get; set; }
    public DateTimeOffset PickedAt { get; set; } = DateTimeOffset.UtcNow;
    public int DisplayOrder { get; set; }

    public bool IsVideo => Type.Equals("VIDEO", StringComparison.OrdinalIgnoreCase) ||
        MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
        ? Path.GetFileNameWithoutExtension(Filename)
        : Title;

    public string MediaUrl => LocalPath;

    public string ThumbnailUrl => string.IsNullOrWhiteSpace(LocalPath)
        ? ""
        : LocalPath;
}
