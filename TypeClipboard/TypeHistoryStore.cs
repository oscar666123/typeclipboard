using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeClipboard;

internal sealed class TypeHistoryStore
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public List<TypeHistoryItem> Items { get; private set; } = [];

    public async Task LoadAsync()
    {
        string path = GetPath();
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return;
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            List<TypeHistoryItem?>? loadedItems =
                await JsonSerializer.DeserializeAsync<List<TypeHistoryItem?>>(stream, _jsonOptions);
            if (!IsValid(loadedItems))
            {
                throw new JsonException("Type History contains invalid records.");
            }

            Items = loadedItems!.Select(item => item!).ToList();
        }
        catch (JsonException)
        {
            string directory = Path.GetDirectoryName(path)!;
            string corruptedPath = Path.Combine(directory, $"type-history-corrupted-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
            File.Move(path, corruptedPath, overwrite: true);
            Items = [];
        }
    }

    public TypeHistoryItem AddOrReuse(string content, int maximumItems)
    {
        maximumItems = Math.Clamp(maximumItems, 20, 1000);
        DateTimeOffset now = DateTimeOffset.Now;
        TypeHistoryItem? item = Items.FirstOrDefault(candidate => candidate.Content == content);
        if (item is null)
        {
            item = new TypeHistoryItem
            {
                Content = content,
                CreatedAt = now,
                LastUsedAt = now,
                Status = TypeHistoryStatus.Started
            };
            Items.Add(item);
        }
        else
        {
            item.LastUsedAt = now;
            item.UseCount++;
            item.Status = TypeHistoryStatus.Started;
        }

        Trim(maximumItems);
        return item;
    }

    public void Trim(int maximumItems)
    {
        maximumItems = Math.Clamp(maximumItems, 20, 1000);
        while (Items.Count > maximumItems)
        {
            TypeHistoryItem? oldest = Items.Where(item => !item.IsPinned).MinBy(item => item.LastUsedAt);
            if (oldest is null) break;
            Items.Remove(oldest);
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            string path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporaryPath = path + ".tmp";
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, Items, _jsonOptions);
                await stream.FlushAsync();
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static string GetPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TypeClipboard", "type-history.json");

    private static bool IsValid(List<TypeHistoryItem?>? items)
    {
        if (items is null) return false;

        HashSet<Guid> ids = [];
        foreach (TypeHistoryItem? item in items)
        {
            if (item is null ||
                item.Id == Guid.Empty ||
                item.Content is null ||
                string.IsNullOrWhiteSpace(item.Content) ||
                item.CreatedAt == default ||
                item.LastUsedAt == default ||
                item.UseCount < 1 ||
                !Enum.IsDefined(item.Status) ||
                !ids.Add(item.Id))
            {
                return false;
            }
        }

        return true;
    }
}
