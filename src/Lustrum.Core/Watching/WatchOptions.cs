namespace Lustrum.Core.Watching;

public sealed record WatchOptions(TimeSpan Debounce, TimeSpan PollInterval, int MaxWatchers)
{
    // Each FileSystemWatcher is one inotify instance on Linux, and the default per-user limit is 128.
    private static readonly int PlatformMaxWatchers = OperatingSystem.IsLinux() ? 64 : 256;

    public static WatchOptions Default { get; } = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), PlatformMaxWatchers);
}
