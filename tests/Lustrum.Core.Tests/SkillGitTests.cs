using FluentAssertions;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillGitTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();
    private readonly SkillGit _git = new(new GitCli());

    public void Dispose() => _sandbox.Dispose();

    private SkillSource Global(string claudeDir) => new("global", SourceKind.Global, "Global", claudeDir, null, true);

    private (Skill Skill, string Publisher, string Clone) ClonedSkill()
    {
        var (remote, publisher) = _sandbox.Published();
        var clone = _sandbox.Clone(remote, "home/.claude/skills/tool");
        var skill = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(clone, "SKILL.md"));
        return (skill, publisher, clone);
    }

    [Fact]
    public async Task Inspecting_a_cloned_skill_reports_branch_last_commit_and_that_it_is_up_to_date()
    {
        var (skill, _, _) = ClonedSkill();

        var report = (await _git.InspectAsync(skill, default))!;

        report.Repository.Role.Should().Be(RepositoryRole.SkillsOnly);
        report.Tracking.Should().Be(new GitTracking("main", "origin/main", 0, 0));
        report.Changes.Should().BeEmpty();
        report.LastCommit!.Subject.Should().Be("Add skill");
        report.Update.Should().BeOfType<UpToDate>();
    }

    [Fact]
    public async Task A_skill_outside_git_has_no_report()
    {
        var file = GitSandbox.Write(_sandbox.Path("home/.claude/skills/tool"), "SKILL.md", "x");

        (await _git.InspectAsync(GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), file), default)).Should().BeNull();
    }

    [Fact]
    public async Task Checking_then_updating_fast_forwards_the_skill()
    {
        var (skill, publisher, clone) = ClonedSkill();
        _sandbox.Publish(publisher, "SKILL.md", GitSandbox.Skill("second"), "Improve skill");

        var location = _git.Locate(skill)!;
        var checkedTracking = await _git.CheckForUpdatesAsync(location.Repository, default);
        var report = (await _git.InspectAsync(skill, default))!;
        var updated = await _git.UpdateAsync(location.Repository, default);

        checkedTracking.Behind.Should().Be(1);
        report.Update.Should().Be(new UpdateAvailable(1));
        updated.Behind.Should().Be(0);
        File.ReadAllText(Path.Combine(clone, "SKILL.md")).Should().Contain("second");
    }

    [Fact]
    public async Task Updating_refuses_a_dirty_repository_and_leaves_it_alone()
    {
        var (skill, publisher, clone) = ClonedSkill();
        _sandbox.Publish(publisher, "SKILL.md", GitSandbox.Skill("second"), "Improve skill");
        GitSandbox.Write(clone, "draft.md", "mine");

        var update = () => _git.UpdateAsync(_git.Locate(skill)!.Repository, default);

        (await update.Should().ThrowAsync<GitUpdateRefusedException>()).Which.Message.Should().Be("The repository has uncommitted changes");
        File.ReadAllText(Path.Combine(clone, "SKILL.md")).Should().Contain("first");
    }

    [Fact]
    public async Task Updating_refuses_to_overwrite_an_ignored_file_the_upstream_starts_tracking()
    {
        var (remote, publisher) = _sandbox.Published();
        _sandbox.Publish(publisher, ".gitignore", "local/\n", "Ignore local files");
        var clone = _sandbox.Clone(remote, "home/.claude/skills/tool");
        var skill = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(clone, "SKILL.md"));
        GitSandbox.Write(clone, "local/settings.json", "mine");
        _sandbox.Publish(publisher, ".gitignore", "", "Stop ignoring");
        _sandbox.Publish(publisher, "local/settings.json", "theirs", "Track settings");

        var update = () => _git.UpdateAsync(_git.Locate(skill)!.Repository, default);

        (await update.Should().ThrowAsync<GitUpdateRefusedException>()).Which.Message
            .Should().Be("The update would overwrite local/settings.json, which git ignores here");
        File.ReadAllText(Path.Combine(clone, "local", "settings.json")).Should().Be("mine");
    }

    [Fact]
    public async Task Updating_refuses_a_repository_that_holds_more_than_skills()
    {
        var project = _sandbox.Init("storefront");
        _sandbox.Commit(project, ".claude/skills/deploy/SKILL.md", "x", "Add deploy");
        var source = new SkillSource("project", SourceKind.Project, "storefront", Path.Combine(project, ".claude"), null, true);
        var skill = GitSandbox.SkillAt(source, Path.Combine(project, ".claude", "skills", "deploy", "SKILL.md"));

        var report = (await _git.InspectAsync(skill, default))!;
        var update = () => _git.UpdateAsync(report.Repository, default);

        report.Update.Should().BeOfType<UpdateBlocked>();
        await update.Should().ThrowAsync<GitUpdateRefusedException>();
    }

    [Fact]
    public async Task Only_changes_under_the_skill_are_listed_but_any_change_blocks_the_update()
    {
        var (remote, publisher) = _sandbox.Published("tool/SKILL.md");
        var skills = _sandbox.Clone(remote, "home/.claude/skills");
        _sandbox.Publish(publisher, "tool/SKILL.md", GitSandbox.Skill("second"), "Improve");
        GitSandbox.Write(skills, "other/SKILL.md", "untracked");
        var skill = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(skills, "tool", "SKILL.md"));
        await _git.CheckForUpdatesAsync(_git.Locate(skill)!.Repository, default);

        var report = (await _git.InspectAsync(skill, default))!;

        report.Changes.Should().BeEmpty();
        report.Update.Should().Be(new UpdateBlocked("The repository has uncommitted changes"));
    }

    [Fact]
    public async Task Incoming_commits_are_counted_per_skill_folder()
    {
        var (remote, publisher) = _sandbox.Published("tool/SKILL.md");
        _sandbox.Publish(publisher, "other/SKILL.md", GitSandbox.Skill("other"), "Add other");
        var skills = _sandbox.Clone(remote, "home/.claude/skills");
        _sandbox.Publish(publisher, "tool/SKILL.md", GitSandbox.Skill("second"), "Improve tool");
        _sandbox.Publish(publisher, "tool/scripts/run.sh", "echo", "Add script");
        var skill = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(skills, "tool", "SKILL.md"));
        var repository = _git.Locate(skill)!.Repository;

        var tracking = await _git.CheckForUpdatesAsync(repository, default);
        var counts = await _git.IncomingCommitsAsync(repository, ["tool", "other"], default);

        tracking.Behind.Should().Be(2);
        counts.Should().BeEquivalentTo(new Dictionary<string, int> { ["tool"] = 2, ["other"] = 0 });
    }

    [Fact]
    public async Task Inspecting_a_skill_behind_its_upstream_shows_what_the_update_brings()
    {
        var (remote, publisher) = _sandbox.Published("tool/SKILL.md");
        _sandbox.Publish(publisher, "other/SKILL.md", GitSandbox.Skill("other"), "Add other");
        var skills = _sandbox.Clone(remote, "home/.claude/skills");
        _sandbox.Publish(publisher, "tool/SKILL.md", GitSandbox.Skill("second"), "Improve tool");
        _sandbox.Publish(publisher, "tool/scripts/run.sh", "echo", "Add script");
        _sandbox.Publish(publisher, "other/SKILL.md", GitSandbox.Skill("other two"), "Improve other");
        var tool = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(skills, "tool", "SKILL.md"));
        await _git.CheckForUpdatesAsync(_git.Locate(tool)!.Repository, default);

        var incoming = (await _git.InspectAsync(tool, default))!.Incoming!;

        incoming.Commits.Should().Be(2);
        incoming.Files.Should().BeEquivalentTo([new GitChange("tool/SKILL.md", GitChangeKind.Modified), new GitChange("tool/scripts/run.sh", GitChangeKind.Added)]);
        incoming.MainFile.Should().Be("tool/SKILL.md");
        incoming.Before.Should().Contain("name: first");
        incoming.After.Should().Contain("name: second");
    }

    [Fact]
    public async Task Local_commits_are_not_shown_as_coming_in()
    {
        var (remote, publisher) = _sandbox.Published("tool/SKILL.md");
        var skills = _sandbox.Clone(remote, "home/.claude/skills");
        _sandbox.Publish(publisher, "tool/SKILL.md", GitSandbox.Skill("theirs"), "Improve upstream");
        _sandbox.Commit(skills, "tool/notes.md", "mine", "Add local notes");
        _sandbox.Commit(skills, "other/SKILL.md", GitSandbox.Skill("other"), "Add local skill");
        var tool = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(skills, "tool", "SKILL.md"));
        var repository = _git.Locate(tool)!.Repository;
        await _git.CheckForUpdatesAsync(repository, default);

        var incoming = (await _git.InspectAsync(tool, default))!.Incoming!;
        var counts = await _git.IncomingCommitsAsync(repository, ["tool", "other"], default);

        incoming.Files.Should().Equal(new GitChange("tool/SKILL.md", GitChangeKind.Modified));
        incoming.Before.Should().Contain("name: first");
        incoming.After.Should().Contain("name: theirs");
        counts.Should().BeEquivalentTo(new Dictionary<string, int> { ["tool"] = 1, ["other"] = 0 });
    }

    [Fact]
    public async Task Paths_with_spaces_are_followed()
    {
        var (remote, publisher) = _sandbox.Published("my tool/SKILL.md");
        var skills = _sandbox.Clone(remote, "home/.claude/skills");
        _sandbox.Publish(publisher, "my tool/SKILL.md", GitSandbox.Skill("second"), "Improve");
        var tool = GitSandbox.SkillAt(Global(_sandbox.Path("home/.claude")), Path.Combine(skills, "my tool", "SKILL.md"));
        await _git.CheckForUpdatesAsync(_git.Locate(tool)!.Repository, default);

        var incoming = (await _git.InspectAsync(tool, default))!.Incoming!;

        incoming.Commits.Should().Be(1);
        incoming.After.Should().Contain("name: second");
    }

    [Fact]
    public async Task A_skill_that_is_the_whole_repository_counts_every_incoming_commit()
    {
        var (skill, publisher, _) = ClonedSkill();
        _sandbox.Publish(publisher, "SKILL.md", GitSandbox.Skill("second"), "Improve");
        _sandbox.Publish(publisher, "scripts/run.sh", "echo", "Add script");
        await _git.CheckForUpdatesAsync(_git.Locate(skill)!.Repository, default);

        var incoming = (await _git.InspectAsync(skill, default))!.Incoming!;

        incoming.MainFile.Should().Be("SKILL.md");
        incoming.Commits.Should().Be(2);
        incoming.Files.Select(f => f.Path).Should().BeEquivalentTo("SKILL.md", "scripts/run.sh");
    }

    [Fact]
    public async Task An_up_to_date_skill_has_nothing_incoming()
    {
        var (skill, _, _) = ClonedSkill();

        (await _git.InspectAsync(skill, default))!.Incoming.Should().BeNull();
    }

    [Fact]
    public async Task Checking_a_repository_without_upstream_does_not_fetch()
    {
        var repo = _sandbox.Init("home/.claude/skills");
        _sandbox.Commit(repo, "tool/SKILL.md", "x", "Add");
        _sandbox.Git(repo, "remote", "add", "origin", _sandbox.Path("missing.git"));

        var tracking = await _git.CheckForUpdatesAsync(new SkillRepository(repo, RepositoryRole.SkillsOnly), default);

        tracking.Should().Be(new GitTracking("main", null, 0, 0));
    }
}
