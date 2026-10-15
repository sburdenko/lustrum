using Lustrum.Core.Settings;

namespace Lustrum.App.Services;

public interface IThemeService
{
    void Apply(ThemePreference preference);
}
