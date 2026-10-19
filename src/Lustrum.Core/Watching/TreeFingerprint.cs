using System.Security.Cryptography;
using System.Text;

namespace Lustrum.Core.Watching;

public static class TreeFingerprint
{
    private const int MaxDepth = 6;

    public static string Compute(IEnumerable<WatchTarget> targets)
    {
        var builder = new StringBuilder();
        foreach (var target in targets.Where(t => Directory.Exists(t.Path)).OrderBy(t => t.Path, StringComparer.Ordinal))
        {
            foreach (var entry in Entries(target))
            {
                builder.Append(entry).Append('\n');
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    // Folder timestamps are left out: Windows updates them lazily, which made an idle tree look changed. Names catch
    // added and removed folders; file sizes and timestamps catch edits.
    private static string Entry(FileSystemInfo info) =>
        info is FileInfo file ? $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : $"{info.FullName}|dir";

    private static IEnumerable<string> Entries(WatchTarget target)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = target.Recursive,
            MaxRecursionDepth = MaxDepth,
            IgnoreInaccessible = true,
        };

        try
        {
            return new DirectoryInfo(target.Path)
                .EnumerateFileSystemInfos(target.Filter, options)
                .Select(Entry)
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [$"{target.Path}|unreadable"];
        }
    }
}
