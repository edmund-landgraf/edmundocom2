using System.Text.Json;

namespace Edmundocom.Photos;

public sealed class SharedMediaRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly object _lock = new();

    public SharedMediaRepository(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IReadOnlyList<SharedMediaItem> GetAll()
    {
        EnsureFile();

        lock (_lock)
        {
            var json = File.ReadAllText(GetContentPath());
            var items = JsonSerializer.Deserialize<List<SharedMediaItem>>(json, JsonOptions) ?? [];

            return items
                .OrderBy(item => item.DisplayOrder)
                .ThenByDescending(item => item.CreateTime ?? item.PickedAt)
                .ThenBy(item => item.DisplayTitle)
                .ToList();
        }
    }

    public void Upsert(IEnumerable<SharedMediaItem> incoming)
    {
        EnsureFile();

        lock (_lock)
        {
            var current = GetAll().ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            var nextOrder = current.Values.Any() ? current.Values.Max(item => item.DisplayOrder) + 10 : 10;

            foreach (var item in incoming.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
            {
                if (current.TryGetValue(item.Id, out var existing))
                {
                    item.Title = string.IsNullOrWhiteSpace(existing.Title) ? item.Title : existing.Title;
                    item.Description = existing.Description;
                    item.DisplayOrder = existing.DisplayOrder;
                }
                else
                {
                    item.DisplayOrder = nextOrder;
                    nextOrder += 10;
                }

                current[item.Id] = item;
            }

            Save(current.Values);
        }
    }

    public void Remove(string id)
    {
        EnsureFile();

        lock (_lock)
        {
            var items = GetAll()
                .Where(item => !item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Save(items);
        }
    }

    private void Save(IEnumerable<SharedMediaItem> items)
    {
        var ordered = items
            .OrderBy(item => item.DisplayOrder)
            .ThenByDescending(item => item.CreateTime ?? item.PickedAt)
            .ToList();

        File.WriteAllText(GetContentPath(), JsonSerializer.Serialize(ordered, JsonOptions));
    }

    private void EnsureFile()
    {
        var path = GetContentPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, "[]");
        }
    }

    private string GetContentPath() => Path.Combine(_environment.ContentRootPath, "Content", "Photos", "shared-media.json");
}
