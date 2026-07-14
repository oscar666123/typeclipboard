using System.Text.Json;

namespace TypeClipboard;

internal sealed class AppSettings
{
    private const string SettingsFileName = "settings.json";

    public AppSettings()
    {
    }

    public int EmergencyShortcutKeyData { get; set; } = (int)Keys.F8;

    public int TypeShortcutKeyData { get; set; } = (int)Keys.F9;

    public int StopShortcutKeyData { get; set; } = (int)Keys.F10;

    public bool EmergencyShortcutEnabled { get; set; } = true;

    public bool TypeShortcutEnabled { get; set; } = true;

    public bool StopShortcutEnabled { get; set; } = true;

    public bool AlwaysOnTop { get; set; } = true;

    public bool SaveTypeHistory { get; set; } = true;

    public int MaximumHistoryItems { get; set; } = 100;

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
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new AppSettings();
            }

            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (!root.TryGetProperty(nameof(TypeShortcutKeyData), out _))
            {
                settings.TypeShortcutKeyData = ReadLegacyShortcut(
                    root,
                    "TypeShortcutId",
                    Keys.F9,
                    new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ctrl-shift-t"] = Keys.Control | Keys.Shift | Keys.T,
                        ["ctrl-alt-t"] = Keys.Control | Keys.Alt | Keys.T,
                        ["f9"] = Keys.F9
                    });
            }

            if (!root.TryGetProperty(nameof(TypeShortcutEnabled), out _))
            {
                settings.TypeShortcutEnabled = !IsLegacyShortcutDisabled(root, "TypeShortcutId");
            }

            if (!root.TryGetProperty(nameof(StopShortcutKeyData), out _))
            {
                settings.StopShortcutKeyData = ReadLegacyShortcut(
                    root,
                    "StopShortcutId",
                    Keys.F10,
                    new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ctrl-shift-s"] = Keys.Control | Keys.Shift | Keys.S,
                        ["ctrl-alt-s"] = Keys.Control | Keys.Alt | Keys.S,
                        ["f10"] = Keys.F10
                    });
            }

            if (!root.TryGetProperty(nameof(StopShortcutEnabled), out _))
            {
                settings.StopShortcutEnabled = !IsLegacyShortcutDisabled(root, "StopShortcutId");
            }

            settings.MaximumHistoryItems = Math.Clamp(settings.MaximumHistoryItems, 20, 1000);
            return settings;
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

    private static int ReadLegacyShortcut(
        JsonElement root,
        string propertyName,
        Keys fallback,
        IReadOnlyDictionary<string, Keys> mappings)
    {
        if (root.TryGetProperty(propertyName, out JsonElement legacyValue) &&
            legacyValue.ValueKind == JsonValueKind.String &&
            legacyValue.GetString() is string legacyId &&
            mappings.TryGetValue(legacyId, out Keys shortcut))
        {
            return (int)shortcut;
        }

        return (int)fallback;
    }

    private static bool IsLegacyShortcutDisabled(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement legacyValue) &&
               legacyValue.ValueKind == JsonValueKind.String &&
               string.Equals(legacyValue.GetString(), "disabled", StringComparison.OrdinalIgnoreCase);
    }
}
