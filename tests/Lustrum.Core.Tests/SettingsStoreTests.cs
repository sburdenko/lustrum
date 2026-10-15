using FluentAssertions;
using Lustrum.Core.Settings;

namespace Lustrum.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lustrum-settings-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "nested", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_yields_defaults_without_warning()
    {
        var load = SettingsStore.Load(SettingsPath);

        load.Settings.Should().Be(LustrumSettings.Default);
        load.Warning.Should().BeNull();
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var settings = LustrumSettings.Default with
        {
            OwnPrefixes = ["me-", "team-"],
            ExtraRoots = ["/work"],
            SearchDepth = 2,
            EditorCommand = "code",
            Theme = ThemePreference.Dark,
        };

        SettingsStore.Save(SettingsPath, settings);
        var load = SettingsStore.Load(SettingsPath);

        load.Settings.OwnPrefixes.Should().Equal("me-", "team-");
        load.Settings.ExtraRoots.Should().Equal("/work");
        load.Settings.SearchDepth.Should().Be(2);
        load.Settings.EditorCommand.Should().Be("code");
        load.Settings.Theme.Should().Be(ThemePreference.Dark);
    }

    [Fact]
    public void Corrupt_file_yields_defaults_with_warning()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ nope");

        var load = SettingsStore.Load(SettingsPath);

        load.Settings.Should().Be(LustrumSettings.Default);
        load.Warning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Missing_fields_fall_back_to_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "editorCommand": "rider" }""");

        var load = SettingsStore.Load(SettingsPath);

        load.Settings.EditorCommand.Should().Be("rider");
        load.Settings.OwnPrefixes.Should().Equal(LustrumSettings.Default.OwnPrefixes);
        load.Settings.SearchDepth.Should().Be(LustrumSettings.Default.SearchDepth);
    }

    [Fact]
    public void Null_list_entries_and_unknown_theme_are_tolerated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ownPrefixes": [null, "me-"], "extraRoots": [null], "theme": 42 }""");

        var load = SettingsStore.Load(SettingsPath);

        load.Settings.OwnPrefixes.Should().Equal("me-");
        load.Settings.ExtraRoots.Should().BeEmpty();
        load.Settings.Theme.Should().Be(ThemePreference.System);
    }

    [Fact]
    public void Wrong_value_types_yield_defaults_with_warning()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "searchDepth": "deep" }""");

        var load = SettingsStore.Load(SettingsPath);

        load.Settings.Should().Be(LustrumSettings.Default);
        load.Warning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Values_are_normalized()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var raw = LustrumSettings.Default with
        {
            OwnPrefixes = [" sb- ", "", "  "],
            ExtraRoots = ["~/dev", " "],
            SearchDepth = 99,
        };

        var normalized = raw.Normalize();

        normalized.OwnPrefixes.Should().Equal("sb-");
        normalized.ExtraRoots.Should().Equal(Path.Combine(home, "dev"));
        normalized.SearchDepth.Should().Be(LustrumSettings.MaxSearchDepth);
    }

    [Fact]
    public void Settings_map_to_scan_options()
    {
        var options = (LustrumSettings.Default with { OwnPrefixes = ["x-"], SearchDepth = 3 }).ToScanOptions();

        options.OwnPrefixes.Should().Equal("x-");
        options.SearchDepth.Should().Be(3);
        options.ExcludedFolders.Should().Equal(LustrumSettings.Default.ExcludedFolders);
    }
}
