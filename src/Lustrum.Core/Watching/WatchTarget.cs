namespace Lustrum.Core.Watching;

public sealed record WatchTarget(string Path, bool Recursive, string Filter = WatchTarget.All)
{
    public const string All = "*";
}
