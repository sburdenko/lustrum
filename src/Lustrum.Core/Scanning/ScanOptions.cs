namespace Lustrum.Core.Scanning;

public sealed record ScanOptions(IReadOnlyList<string> OwnPrefixes)
{
    public static ScanOptions Default { get; } = new(["sb-"]);
}
