namespace Lustrum.Core.Watching;

/// <summary>
/// Raises <see cref="Changed"/> after files under the targets change, debounced. Uses native file system
/// watchers, and falls back to polling a fingerprint when there are more targets than the platform
/// comfortably supports or when the native watchers cannot be created. Construction may hash the targets,
/// so callers on a UI thread should create it in the background.
/// </summary>
public sealed class SkillTreeWatcher : IDisposable
{
    private readonly DebouncedTrigger _trigger;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly IReadOnlyList<WatchTarget> _targets;
    private Timer? _poller;
    private string _fingerprint = string.Empty;
    private volatile bool _disposed;

    public SkillTreeWatcher(IReadOnlyList<WatchTarget> targets, WatchOptions options)
    {
        _targets = targets.Where(t => Directory.Exists(t.Path)).Distinct().ToList();
        _trigger = new DebouncedTrigger(options.Debounce, () => Changed?.Invoke());

        if (_targets.Count > options.MaxWatchers || !TryWatchNatively())
        {
            IsPolling = true;
            _fingerprint = TreeFingerprint.Compute(_targets);
            _poller = new Timer(_ => Poll(), null, options.PollInterval, options.PollInterval);
        }
    }

    public event Action? Changed;

    public bool IsPolling { get; }

    public int WatchedCount => _targets.Count;

    public void Dispose()
    {
        _disposed = true;
        _trigger.Dispose();
        _poller?.Dispose();
        DisposeWatchers();
    }

    private bool TryWatchNatively()
    {
        try
        {
            foreach (var target in _targets)
            {
                _watchers.Add(CreateWatcher(target));
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            DisposeWatchers();
            return false;
        }
    }

    private FileSystemWatcher CreateWatcher(WatchTarget target)
    {
        var watcher = new FileSystemWatcher(target.Path, target.Filter)
        {
            IncludeSubdirectories = target.Recursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        watcher.Created += (_, e) => SignalUnlessHidden(target, e.FullPath);
        watcher.Changed += (_, e) => SignalUnlessHidden(target, e.FullPath);
        watcher.Deleted += (_, e) => SignalUnlessHidden(target, e.FullPath);
        watcher.Renamed += (_, e) =>
        {
            if (!IsHidden(target, e.FullPath) || !IsHidden(target, e.OldFullPath))
            {
                _trigger.Signal();
            }
        };
        watcher.Error += (_, _) => _trigger.Signal();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void SignalUnlessHidden(WatchTarget target, string path)
    {
        if (!IsHidden(target, path))
        {
            _trigger.Signal();
        }
    }

    // Sync staging folders and Finder metadata change constantly and never hold skills.
    private static bool IsHidden(WatchTarget target, string path) =>
        Path.GetRelativePath(target.Path, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.StartsWith('.') && segment is not "." and not "..");

    private void Poll()
    {
        if (_disposed)
        {
            return;
        }

        var current = TreeFingerprint.Compute(_targets);
        if (Interlocked.Exchange(ref _fingerprint, current) != current)
        {
            _trigger.Signal();
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }
}
