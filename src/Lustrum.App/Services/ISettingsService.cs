using Lustrum.Core.Settings;

namespace Lustrum.App.Services;

public interface ISettingsService
{
    LustrumSettings Current { get; }

    string? Warning { get; }

    string LogFolder { get; }

    bool TrySave(LustrumSettings settings, out string error);
}
