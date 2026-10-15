using Lustrum.App.Services;
using Lustrum.Core.Settings;

namespace Lustrum.App.Tests;

internal sealed class FakeShell : IShellService
{
    public List<string> Revealed { get; } = [];

    public List<string> Opened { get; } = [];

    public List<(string Path, string Command)> Edited { get; } = [];

    public bool Succeeds { get; set; } = true;

    public bool OpenFolder(string path)
    {
        Opened.Add(path);
        return Succeeds;
    }

    public bool RevealFile(string path)
    {
        Revealed.Add(path);
        return Succeeds;
    }

    public bool OpenInEditor(string path, string command)
    {
        Edited.Add((path, command));
        return Succeeds;
    }
}

internal sealed class FakeClipboard : IClipboardService
{
    public List<string> Texts { get; } = [];

    public Task SetTextAsync(string text)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSettings(LustrumSettings initial, string? warning = null) : ISettingsService
{
    public LustrumSettings Current { get; private set; } = initial;

    public string? Warning { get; } = warning;

    public List<LustrumSettings> Saved { get; } = [];

    public string LogFolder => "/logs";

    public bool FailSave { get; set; }

    public bool TrySave(LustrumSettings settings, out string error)
    {
        error = FailSave ? "disk full" : string.Empty;
        if (FailSave)
        {
            return false;
        }

        Saved.Add(settings);
        Current = settings;
        return true;
    }
}

internal sealed class FakeTheme : IThemeService
{
    public List<ThemePreference> Applied { get; } = [];

    public void Apply(ThemePreference preference) => Applied.Add(preference);
}
