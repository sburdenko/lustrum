using FluentAssertions;
using Lustrum.Core.Git;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class GitCliTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();
    private readonly GitCli _git = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public async Task A_fresh_clone_tracks_its_upstream_and_is_level_with_it()
    {
        var (remote, _) = _sandbox.Published();
        var clone = _sandbox.Clone(remote, "clone");

        var tracking = await _git.TrackingAsync(clone, default);

        tracking.Should().Be(new GitTracking("main", "origin/main", 0, 0));
    }

    [Fact]
    public async Task Behind_counts_appear_only_after_a_fetch()
    {
        var (remote, publisher) = _sandbox.Published();
        var clone = _sandbox.Clone(remote, "clone");
        _sandbox.Publish(publisher, "SKILL.md", GitSandbox.Skill("second"), "Update");
        _sandbox.Publish(publisher, "notes.md", "n", "Notes");

        (await _git.TrackingAsync(clone, default)).Behind.Should().Be(0);
        await _git.FetchAsync(clone, default);

        (await _git.TrackingAsync(clone, default)).Should().Be(new GitTracking("main", "origin/main", 0, 2));
    }

    [Fact]
    public async Task Local_commits_count_as_ahead()
    {
        var (remote, _) = _sandbox.Published();
        var clone = _sandbox.Clone(remote, "clone");
        _sandbox.Commit(clone, "local.md", "x", "Local");

        (await _git.TrackingAsync(clone, default)).Ahead.Should().Be(1);
    }

    [Fact]
    public async Task A_branch_without_upstream_and_a_detached_head_are_told_apart()
    {
        var repo = _sandbox.Init("local");
        _sandbox.Commit(repo, "SKILL.md", "x", "First");
        _sandbox.Commit(repo, "SKILL.md", "y", "Second");

        (await _git.TrackingAsync(repo, default)).Should().Be(new GitTracking("main", null, 0, 0));

        _sandbox.Git(repo, "checkout", "--quiet", "HEAD~1");
        (await _git.TrackingAsync(repo, default)).Should().Be(GitTracking.Detached);
    }

    [Fact]
    public async Task Changes_can_be_limited_to_one_skill_folder()
    {
        var repo = _sandbox.Init("skills");
        _sandbox.Commit(repo, "alpha/SKILL.md", "a", "Alpha");
        _sandbox.Commit(repo, "beta/SKILL.md", "b", "Beta");
        GitSandbox.Write(repo, "alpha/SKILL.md", "changed");
        GitSandbox.Write(repo, "alpha/extra/notes.md", "new");
        GitSandbox.Write(repo, "beta/SKILL.md", "changed");

        var alpha = await _git.ChangesAsync(repo, "alpha", default);
        var all = await _git.ChangesAsync(repo, null, default);

        alpha.Should().BeEquivalentTo([
            new GitChange("alpha/SKILL.md", GitChangeKind.Modified),
            new GitChange("alpha/extra/notes.md", GitChangeKind.Untracked)]);
        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task Last_commit_is_the_latest_one_touching_the_path()
    {
        var repo = _sandbox.Init("skills");
        _sandbox.Commit(repo, "alpha/SKILL.md", "a", "Add alpha");
        _sandbox.Commit(repo, "beta/SKILL.md", "b", "Add beta");

        var alpha = await _git.LastCommitAsync(repo, "alpha", default);
        var whole = await _git.LastCommitAsync(repo, ".", default);

        alpha!.Subject.Should().Be("Add alpha");
        alpha.Author.Should().Be("Test");
        alpha.ShortHash.Length.Should().BeInRange(7, 12);
        alpha.Hash.Should().StartWith(alpha.ShortHash);
        whole!.Subject.Should().Be("Add beta");
    }

    [Fact]
    public async Task A_repository_without_commits_has_no_last_commit()
    {
        var repo = _sandbox.Init("empty");

        (await _git.LastCommitAsync(repo, ".", default)).Should().BeNull();
        (await _git.TrackingAsync(repo, default)).Should().Be(new GitTracking("main", null, 0, 0));
    }

    [Fact]
    public async Task Fast_forward_brings_the_upstream_changes_into_the_folder()
    {
        var (remote, publisher) = _sandbox.Published();
        var clone = _sandbox.Clone(remote, "clone");
        _sandbox.Publish(publisher, "SKILL.md", GitSandbox.Skill("second"), "Update");
        await _git.FetchAsync(clone, default);

        await _git.FastForwardAsync(clone, default);

        File.ReadAllText(Path.Combine(clone, "SKILL.md")).Should().Contain("second");
    }

    [Fact]
    public async Task A_failing_command_reports_what_git_said()
    {
        var repo = _sandbox.Init("local");
        _sandbox.Commit(repo, "SKILL.md", "x", "First");
        _sandbox.Git(repo, "remote", "add", "origin", _sandbox.Path("does-not-exist.git"));

        var fetch = () => _git.FetchAsync(repo, default);

        (await fetch.Should().ThrowAsync<GitException>()).Which.Message.Should().StartWith("git fetch failed: ");
    }

    [Fact]
    public async Task A_missing_git_executable_is_reported_as_unavailable()
    {
        var git = new GitCli("git-that-does-not-exist-" + Guid.NewGuid().ToString("N"));

        var tracking = () => git.TrackingAsync(_sandbox.Root, default);

        await tracking.Should().ThrowAsync<GitUnavailableException>();
    }
}
