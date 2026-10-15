using Avalonia;
using Avalonia.Styling;
using Lustrum.Core.Settings;

namespace Lustrum.App.Services;

public sealed class ThemeService : IThemeService
{
    public void Apply(ThemePreference preference)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = preference switch
            {
                ThemePreference.Light => ThemeVariant.Light,
                ThemePreference.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
