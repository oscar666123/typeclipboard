using System.Text.Json;

namespace TypeClipboard;

internal sealed class AppSettings
{
    private const string SettingsFileName = "settings.json";

    public AppSettings()
    {
    }

    public string TypeShortcutId { get; set; } = "ctrl-t";

    public string StopShortcutId { get; set; } = "escape";

    public bool AlwaysOnTop { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            string path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        string path = GetSettingsPath();
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp";
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TypeClipboard",
            SettingsFileName);
    }
}
