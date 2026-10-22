using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace Lustrum.Core.ClaudeSettings;

/// <summary>
/// Reads the com.anthropic.claudecode configuration profile on macOS (converted with plutil, as Claude Code does)
/// and the Settings value under SOFTWARE\Policies\ClaudeCode in HKLM and HKCU on Windows. A converted profile is
/// kept until the file changes, so a rescan costs one file check, not a process.
/// </summary>
public sealed class SystemPolicyStore(string userName, string plutil = "/usr/bin/plutil") : IPolicyStore
{
    public const string ProfilesFolder = "/Library/Managed Preferences";
    private const string Domain = "com.anthropic.claudecode";
    private const string RegistryPath = @"SOFTWARE\Policies\ClaudeCode";
    private const string RegistryValue = "Settings";
    private static readonly TimeSpan PlutilTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, (DateTime Written, long Length, PolicyText Text)> _profiles = new(StringComparer.Ordinal);

    public static SystemPolicyStore Instance { get; } = new(Environment.UserName);

    public IReadOnlyList<PolicyText> DevicePolicies()
    {
        if (OperatingSystem.IsMacOS())
        {
            return ProfilePaths().Where(File.Exists).Select(Profile).ToList();
        }

        if (OperatingSystem.IsWindows())
        {
            return ReadRegistry(Registry.LocalMachine, @"HKLM\" + RegistryPath) is { } policy ? [policy] : [];
        }

        return [];
    }

    public PolicyText? UserPolicy() =>
        OperatingSystem.IsWindows() ? ReadRegistry(Registry.CurrentUser, @"HKCU\" + RegistryPath) : null;

    // The device-wide profile first, then the one installed for this user, which wins where both set a key.
    private IEnumerable<string> ProfilePaths()
    {
        yield return Path.Combine(ProfilesFolder, Domain + ".plist");
        yield return Path.Combine(ProfilesFolder, userName, Domain + ".plist");
    }

    private PolicyText Profile(string path)
    {
        var file = new FileInfo(path);
        if (_profiles.TryGetValue(path, out var cached) && cached.Written == file.LastWriteTimeUtc && cached.Length == file.Length)
        {
            return cached.Text;
        }

        var text = ReadProfile(path, plutil);
        _profiles[path] = (file.LastWriteTimeUtc, file.Length, text);
        return text;
    }

    /// <summary>A configuration profile converted to JSON with plutil.</summary>
    public static PolicyText ReadProfile(string path, string plutil = "/usr/bin/plutil")
    {
        var info = new ProcessStartInfo(plutil)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in (string[])["-convert", "json", "-o", "-", path])
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new PolicyText(path, null, $"plutil could not run: {ex.Message}");
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(PlutilTimeout))
            {
                Kill(process);
                return new PolicyText(path, null, $"plutil did not finish within {PlutilTimeout.TotalSeconds:0} s");
            }

            if (process.ExitCode == 0)
            {
                return new PolicyText(path, output.Result);
            }

            var message = error.Result.Trim();
            return new PolicyText(path, null, message.Length > 0 ? message : $"plutil exited with {process.ExitCode}");
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static PolicyText? ReadRegistry(RegistryKey hive, string label)
    {
        try
        {
            using var key = hive.OpenSubKey(RegistryPath);
            return key?.GetValue(RegistryValue) switch
            {
                null => null,
                string text => new PolicyText(label, text),
                _ => new PolicyText(label, null, "the Settings value is not a string"),
            };
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new PolicyText(label, null, ex.Message);
        }
    }
}
