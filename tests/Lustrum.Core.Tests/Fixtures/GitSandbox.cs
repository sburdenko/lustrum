using System.Diagnostics;
using Lustrum.Core.Models;

namespace Lustrum.Core.Tests.Fixtures;

/// <summary>Temporary folders with real git repositories, isolated from the machine's git configuration.</summary>
public sealed class GitSandbox : IDisposable
{
    private static readonly string EmptyConfig = System.IO.Path.Combine(FakeClaudeHome.LongTempPath(), "lustrum-empty-gitconfig");

    // The code under test starts git itself, so the machine's global and system config is hidden from the whole process.
    static GitSandbox()
    {
        File.WriteAllText(EmptyConfig, string.Empty);
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", EmptyConfig);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
    }

    public GitSandbox()
    {
        Root = System.IO.Path.Combine(FakeClaudeHome.LongTempPath(), "lustrum-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Path(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string Init(string relative)
    {
        var dir = Path(relative);
        Directory.CreateDirectory(dir);
        Git(dir, "init", "--quiet", "-b", "main");
        return dir;
    }

    public string Bare(string relative)
    {
        var dir = Path(relative);
        Directory.CreateDirectory(dir);
        Git(dir, "init", "--quiet", "--bare", "-b", "main");
        return dir;
    }

    public string Clone(string remote, string relative)
    {
        var dir = Path(relative);
        Git(Root, "clone", "--quiet", remote, dir);
        return dir;
    }

    /// <summary>A bare remote with one pushed commit, plus a second clone that can publish more commits.</summary>
    public (string Remote, string Publisher) Published(string skillFile = "SKILL.md")
    {
        var remote = Bare("remote.git");
        var publisher = Clone(remote, "publisher");
        Commit(publisher, skillFile, Skill("first"), "Add skill");
        Git(publisher, "push", "--quiet", "origin", "main");
        return (remote, publisher);
    }

    public void Publish(string publisher, string file, string content, string message)
    {
        Commit(publisher, file, content, message);
        Git(publisher, "push", "--quiet", "origin", "main");
    }

    public void Commit(string repo, string file, string content, string message)
    {
        Write(repo, file, content);
        Git(repo, "add", "--all");
        Git(repo, "commit", "--quiet", "-m", message);
    }

    public static string Write(string repo, string file, string content)
    {
        var path = System.IO.Path.Combine(repo, file.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public static string Skill(string name) => $"---\nname: {name}\ndescription: {name} skill\n---\nBody\n";

    public static Skill SkillAt(SkillSource source, string skillFile, SkillKind kind = SkillKind.Skill)
    {
        var name = kind == SkillKind.Command
            ? System.IO.Path.GetFileNameWithoutExtension(skillFile)
            : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(skillFile))!;
        return new Skill(
            $"{source.Id}#{name}", name, name, "desc", kind, source, skillFile, null,
            new Dictionary<string, string>(), [], [], 1, DateTimeOffset.UnixEpoch, Health.Ok, []);
    }

    public string Git(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", .. arguments])
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stderr}");
        }

        return stdout.Result;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
