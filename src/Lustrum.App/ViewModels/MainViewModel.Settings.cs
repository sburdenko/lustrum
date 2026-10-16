using CommunityToolkit.Mvvm.Input;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    public SettingsEditorViewModel SettingsEditor { get; } = new();

    [RelayCommand]
    private void ResetSettingsEditor() => SettingsEditor.Load(_settings);

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        var settings = SettingsEditor.ToSettings();
        if (!_services.Settings.TrySave(settings, out var error))
        {
            ErrorMessage = $"Settings could not be saved: {error}";
            return;
        }

        _settings = settings;
        _persistentNotice = null;
        SettingsEditor.Load(settings);
        _services.Theme.Apply(settings.Theme);
        Chips = BuildChips(Chips.Where(c => c.IsOn).Select(c => c.Label).ToList());
        await RescanAsync();
    }
}
