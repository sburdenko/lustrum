using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lustrum.Core.Settings;

public sealed record SettingsLoad(LustrumSettings Settings, string? Warning);

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lustrum", "settings.json");

    public static SettingsLoad Load(string path)
    {
        if (!File.Exists(path))
        {
            return new SettingsLoad(LustrumSettings.Default, null);
        }

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(path), Json);
            return new SettingsLoad(ToSettings(document).Normalize(), null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SettingsLoad(LustrumSettings.Default, $"Settings could not be read, defaults are used: {ex.Message}");
        }
    }

    public static void Save(string path, LustrumSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(ToDocument(settings.Normalize()), Json));
        File.Move(temporary, path, overwrite: true);
    }

    private static LustrumSettings ToSettings(SettingsDocument? document)
    {
        var defaults = LustrumSettings.Default;
        return document is null
            ? defaults
            : new LustrumSettings(
                document.OwnPrefixes ?? defaults.OwnPrefixes,
                document.ExtraRoots ?? defaults.ExtraRoots,
                document.SearchDepth ?? defaults.SearchDepth,
                document.ExcludedFolders ?? defaults.ExcludedFolders,
                document.EditorCommand ?? defaults.EditorCommand,
                document.Theme ?? defaults.Theme);
    }

    private static SettingsDocument ToDocument(LustrumSettings settings) => new()
    {
        OwnPrefixes = settings.OwnPrefixes.ToList(),
        ExtraRoots = settings.ExtraRoots.ToList(),
        SearchDepth = settings.SearchDepth,
        ExcludedFolders = settings.ExcludedFolders.ToList(),
        EditorCommand = settings.EditorCommand,
        Theme = settings.Theme,
    };

    private sealed class SettingsDocument
    {
        public List<string>? OwnPrefixes { get; init; }

        public List<string>? ExtraRoots { get; init; }

        public int? SearchDepth { get; init; }

        public List<string>? ExcludedFolders { get; init; }

        public string? EditorCommand { get; init; }

        public ThemePreference? Theme { get; init; }
    }
}
