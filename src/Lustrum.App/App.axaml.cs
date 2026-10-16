using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.App.Views;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App;

public sealed class App : Application
{
    public static Stopwatch SinceStart { get; } = Stopwatch.StartNew();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService(SettingsStore.DefaultPath);
            var log = new ScanLog(settings.LogFolder, SinceStart);
            var paths = ClaudePaths.ForCurrentUser();
            var window = new MainWindow();
            var usage = new UsageCollector();
            var viewModel = new MainViewModel(
                (options, token) => Task.Run(() => SkillScanner.ScanAsync(paths, options, token), token),
                token => Task.Run(() => usage.CollectAsync(paths.ProjectsDir, token), token),
                new AppServices(new ShellService(), new AvaloniaClipboardService(window), settings, new ThemeService()));
            var live = new LiveRescan(viewModel, paths);
            viewModel.ScanApplied += log.Record;
            if (settings.Warning is { } warning)
            {
                log.Info(warning);
            }

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                live.Dispose();
                log.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
