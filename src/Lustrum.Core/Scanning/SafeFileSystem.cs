using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class SafeFileSystem
{
    private static readonly EnumerationOptions NoLinks = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false,
    };

    public static IReadOnlyList<string> Directories(string path, Action<ScanIssue> report)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(path).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            report(new ScanIssue(path, $"Cannot list folder: {ex.Message}"));
            return [];
        }
    }

    public static IReadOnlyList<string> RealDirectories(string path, Action<ScanIssue> report)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(path, "*", NoLinks).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            report(new ScanIssue(path, $"Cannot list folder: {ex.Message}"));
            return [];
        }
    }

    public static IReadOnlyList<string> Files(string path, string pattern, EnumerationOptions options, Action<ScanIssue> report)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(path, pattern, options).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            report(new ScanIssue(path, $"Cannot list folder: {ex.Message}"));
            return [];
        }
    }
}
