using Lustrum.Core.Settings;

namespace Lustrum.App.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _path;

    public SettingsService(string path)
    {
        _path = path;
        var load = SettingsStore.Load(path);
        Current = load.Settings;
        Warning = load.Warning;
    }

    public LustrumSettings Current { get; private set; }

    public string? Warning { get; }

    public string LogFolder => Path.Combine(Path.GetDirectoryName(_path)!, "logs");

    public bool TrySave(LustrumSettings settings, out string error)
    {
        try
        {
            SettingsStore.Save(_path, settings);
            Current = settings;
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
