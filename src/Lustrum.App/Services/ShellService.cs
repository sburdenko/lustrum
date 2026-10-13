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
