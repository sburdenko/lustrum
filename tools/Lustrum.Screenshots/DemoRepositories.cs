using System.Diagnostics;

namespace Lustrum.Screenshots;

/// <summary>Turns one global skill into a clone whose upstream has moved on, so the card can show an update.</summary>
internal sealed partial class DemoHome
{
    private void GitRepositories()
    {
        var remote = Path.Combine(Home, "remotes", "pdf-forms.git");
        var publisher = Path.Combine(Home, "remotes", "pdf-forms-work");
        var skill = Path.Combine(Claude, "skills", "pdf-forms");
        var original = File.ReadAllText(Path.Combine(skill, "SKILL.md"));
        Directory.CreateDirectory(remote);
        Git(remote, "2026-09-12T09:00:00Z", "init", "--quiet", "--bare", "-b", "main");
        Git(Home, "2026-09-12T09:00:00Z", "clone", "--quiet", remote, publisher);

        Publish(publisher, "SKILL.md", original.Replace("version: 1.2.0", "version: 1.0.0"), "Add pdf-forms", "2026-09-12T09:00:00Z");
        Publish(publisher, "SKILL.md", original, "Map fields by their visible label", "2026-09-30T16:20:00Z");
        Directory.Delete(skill, recursive: true);
        Git(Home, "2026-09-30T16:20:00Z", "clone", "--quiet", remote, skill);

        Publish(publisher, "SKILL.md", original.Replace("version: 1.2.0", "version: 1.3.0"), "Support checkbox groups", "2026-10-08T11:05:00Z");
        Publish(publisher, "examples/invoice.json", "{}\n", "Add an invoice example", "2026-10-14T08:40:00Z");
    }

    private static void Publish(string repo, string file, string content, string message, string at)
    {
        Write(Path.Combine(repo, file.Replace('/', Path.DirectorySeparatorChar)), content);
        Git(repo, at, "add", "--all");
        Git(repo, at, "commit", "--quiet", "-m", message);
        Git(repo, at, "push", "--quiet", "origin", "main");
    }

    private static void Git(string cwd, string at, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in (string[])["-c", "user.name=Dana Reyes", "-c", "user.email=dana@example.com", "-c", "commit.gpgsign=false", .. arguments])
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["GIT_AUTHOR_DATE"] = at;
        info.Environment["GIT_COMMITTER_DATE"] = at;
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {error}");
        }
    }
}
