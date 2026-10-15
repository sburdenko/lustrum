using System.ComponentModel;
using System.Diagnostics;

namespace Lustrum.App.Services;

public sealed class ShellService : IShellService
{
    public bool OpenFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return Start("explorer.exe", path);
        }

        return Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", path);
    }

    public bool RevealFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return Start("explorer.exe", "/select," + path);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Start("open", "-R", path);
        }

        return OpenFolder(Path.GetDirectoryName(path)!);
    }

    public bool OpenInEditor(string path, string command)
    {
        var parts = CommandLine.Split(command);
        if (parts.Count > 0)
        {
            return Start(parts[0], [.. parts.Skip(1), path]);
        }

        if (OperatingSystem.IsWindows())
        {
            return StartWithShell(path);
        }

        return Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", path);
    }

    private static bool StartWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }

    private static bool Start(string fileName, params string[] arguments)
    {
        var info = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(info)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }
}
