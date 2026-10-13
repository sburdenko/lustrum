using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.App.Views;
using Lustrum.Core.Scanning;

namespace Lustrum.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var paths = ClaudePaths.ForCurrentUser();
            window.DataContext = new MainViewModel(
                token => Task.Run(() => SkillScanner.ScanAsync(paths, cancellationToken: token), token),
                new ShellService(),
                new AvaloniaClipboardService(window));
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
