using Lustrum.App.Services;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;

namespace Lustrum.App.Tests;

internal sealed class FakeShell : IShellService
{
    public List<string> Revealed { get; } = [];

    public List<string> Opened { get; } = [];

    public List<(string Path, string Command)> Edited { get; } = [];

    public bool Succeeds { get; set; } = true;

    public bool OpenFolder(string path)
    {
        Opened.Add(path);
        return Succeeds;
    }

    public bool RevealFile(string path)
    {
        Revealed.Add(path);
        return Succeeds;
    }

    public bool OpenInEditor(string path, string command)
    {
        Edited.Add((path, command));
        return Succeeds;
    }
}

internal sealed class FakeClipboard : IClipboardService
{
    public List<string> Texts { get; } = [];

    public Task SetTextAsync(string text)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSettings(LustrumSettings initial, string? warning = null) : ISettingsService
{
    public LustrumSettings Current { get; private set; } = initial;

    public string? Warning { get; } = warning;

    public List<LustrumSettings> Saved { get; } = [];

    public string LogFolder => "/logs";

    public bool FailSave { get; set; }

    public bool TrySave(LustrumSettings settings, out string error)
    {
        error = FailSave ? "disk full" : string.Empty;
        if (FailSave)
        {
            return false;
        }

        Saved.Add(settings);
        Current = settings;
        return true;
    }
}

internal sealed class FakeTheme : IThemeService
{
    public List<ThemePreference> Applied { get; } = [];

    public void Apply(ThemePreference preference) => Applied.Add(preference);
}

internal sealed class FakeSkillGit : ISkillGit
{
    public Dictionary<string, SkillGitReport> Reports { get; } = [];

    public Dictionary<string, GitTracking> Checked { get; } = [];

    public Exception? InspectFailure { get; set; }

    public Exception? CheckFailure { get; set; }

    public Exception? UpdateFailure { get; set; }

    public TaskCompletionSource<GitTracking>? PendingUpdate { get; set; }

    public List<string> Inspected { get; } = [];

    public List<string> Fetched { get; } = [];

    public List<string> Updated { get; } = [];

    public SkillLocation? Locate(Skill skill) => Reports.GetValueOrDefault(skill.Id)?.Location;

    public Task<SkillGitReport?> InspectAsync(Skill skill, CancellationToken cancellationToken)
    {
        Inspected.Add(skill.Id);
        return InspectFailure is { } failure
            ? Task.FromException<SkillGitReport?>(failure)
            : Task.FromResult(Reports.GetValueOrDefault(skill.Id));
    }

    public Task<GitTracking> CheckForUpdatesAsync(SkillRepository repository, CancellationToken cancellationToken)
    {
        Fetched.Add(repository.Root);
        if (CheckFailure is { } failure)
        {
            return Task.FromException<GitTracking>(failure);
        }

        var tracking = Checked.GetValueOrDefault(repository.Root, new GitTracking("main", "origin/main", 0, 0));
        foreach (var (id, report) in Reports.Where(p => p.Value.Repository == repository).ToList())
        {
            Reports[id] = report with { Tracking = tracking, Update = UpdatePolicy.Evaluate(repository, tracking, report.Changes) };
        }

        return Task.FromResult(tracking);
    }

    public Task<GitTracking> UpdateAsync(SkillRepository repository, CancellationToken cancellationToken)
    {
        Updated.Add(repository.Root);
        if (PendingUpdate is { } pending)
        {
            return pending.Task;
        }

        return UpdateFailure is { } failure
            ? Task.FromException<GitTracking>(failure)
            : Task.FromResult(new GitTracking("main", "origin/main", 0, 0));
    }
}
