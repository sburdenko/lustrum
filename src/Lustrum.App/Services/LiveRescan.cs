using Avalonia.Threading;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Watching;

namespace Lustrum.App.Services;

public sealed class LiveRescan : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly ClaudePaths _paths;
    private SkillTreeWatcher? _watcher;
    private IReadOnlyList<WatchTarget> _targets = [];
    private IReadOnlyList<WatchTarget> _pending = [];
    private int _generation;

    public LiveRescan(MainViewModel viewModel, ClaudePaths paths)
    {
        _viewModel = viewModel;
        _paths = paths;
        viewModel.ScanApplied += Retarget;
    }

    public void Dispose()
    {
        _viewModel.ScanApplied -= Retarget;
        _generation++;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void Retarget(ScanResult result)
    {
        var targets = WatchTargets.From(result, _paths);
        if (targets.SequenceEqual(_pending) || (_watcher is not null && targets.SequenceEqual(_targets)))
        {
            return;
        }

        _pending = targets;
        _ = RebuildAsync(targets, ++_generation);
    }

    private async Task RebuildAsync(IReadOnlyList<WatchTarget> targets, int generation)
    {
        SkillTreeWatcher watcher;
        try
        {
            watcher = await Task.Run(() => new SkillTreeWatcher(targets, WatchOptions.Default));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (generation == _generation)
            {
                Replace(null, []);
                _viewModel.WatchStatus = $"Live updates off: {ex.Message}";
            }

            return;
        }

        if (generation != _generation)
        {
            watcher.Dispose();
            return;
        }

        watcher.Changed += () => Dispatcher.UIThread.Post(() => _ = _viewModel.RescanFromWatcherAsync());
        Replace(watcher, targets);
        _viewModel.WatchStatus = watcher.IsPolling
            ? $"Checking {watcher.WatchedCount} folders every 30 s"
            : $"Watching {watcher.WatchedCount} folders";
    }

    private void Replace(SkillTreeWatcher? watcher, IReadOnlyList<WatchTarget> targets)
    {
        _watcher?.Dispose();
        _watcher = watcher;
        _targets = targets;
        _pending = [];
    }
}
