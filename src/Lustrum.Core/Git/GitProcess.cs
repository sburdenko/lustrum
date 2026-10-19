using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Lustrum.Core.Git;

internal sealed record GitOutput(int ExitCode, string Stdout, string Stderr)
{
    public bool Succeeded => ExitCode == 0;

    public string FirstErrorLine => Stderr
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault() ?? $"exit code {ExitCode}";
}

internal sealed class GitProcess(string executable)
{
    private static readonly string[] InheritedRepositoryVariables = ["GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_PREFIX"];

    public async Task<GitOutput> RunAsync(string root, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = StartInfo(root, arguments) };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new GitUnavailableException($"git could not be started: {ex.Message}");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new GitOutput(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new GitException($"git {arguments[0]} did not finish within {timeout.TotalSeconds:0} s");
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    private ProcessStartInfo StartInfo(string root, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in (string[])["-C", root, "-c", "core.quotepath=off", "-c", "core.fsmonitor=false", .. arguments])
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var variable in InheritedRepositoryVariables)
        {
            info.Environment.Remove(variable);
        }

        // Never block on a credential prompt nobody can see, keep messages in English for the UI,
        // and let status skip the index refresh so reading never writes to .git.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["SSH_ASKPASS_REQUIRE"] = "never";
        info.Environment["GCM_INTERACTIVE"] = "never";
        info.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        info.Environment["LC_ALL"] = "C";
        return info;
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
        }
    }
}
