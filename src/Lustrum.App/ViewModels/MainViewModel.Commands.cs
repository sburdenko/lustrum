using CommunityToolkit.Mvvm.Input;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private void OpenFolder()
    {
        if (SelectedSkill is { } item && !_services.Shell.RevealFile(item.Skill.Path))
        {
            ErrorMessage = "Could not open the system file manager";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private void OpenInEditor()
    {
        if (SelectedSkill is { } item && !_services.Shell.OpenInEditor(item.Skill.Path, _settings.EditorCommand))
        {
            ErrorMessage = string.IsNullOrEmpty(_settings.EditorCommand)
                ? "Could not open the file with the default app"
                : $"Could not run the editor command '{_settings.EditorCommand}'";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private Task CopyPathAsync() =>
        SelectedSkill is { } item ? _services.Clipboard.SetTextAsync(item.Skill.Path) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private Task CopyInvocationAsync() =>
        SelectedSkill is { } item ? _services.Clipboard.SetTextAsync(item.Skill.InvocationName) : Task.CompletedTask;

    [RelayCommand]
    private void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(_services.Settings.LogFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"Could not create the log folder: {ex.Message}";
            return;
        }

        if (!_services.Shell.OpenFolder(_services.Settings.LogFolder))
        {
            ErrorMessage = "Could not open the log folder";
        }
    }

    public string EditorHint => string.IsNullOrEmpty(_settings.EditorCommand) ? "default app" : _settings.EditorCommand;

    private bool HasSelectedSkill() => SelectedSkill is not null;
}
