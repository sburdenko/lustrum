using FluentAssertions;
using Lustrum.Core.Effective;
using Lustrum.Core.Git;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class WorktreeTests : IDisposable
{
    private readonly GitSandbox _git = new();
    private readonly FakeClaudeHome _home = new();

    public void Dispose()
    {
        _git.Dispose();
        _home.Dispose();
    }

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private (string Main, string Worktree) MainWithWorktree()
    {
        var main = _git.Init("main");
        _git.Commit(main, "README.md", "x", "First");
        GitSandbox.Write(main, ".claude/skills/local/SKILL.md", Doc("local"));
        var worktree = _git.Path("feature");
        _git.Git(main, "worktree", "add", "--quiet", worktree);
        return (main, worktree);
    }

    [Fact]
    public void A_linked_worktree_leads_back_to_its_main_checkout()
    {
        var (main, worktree) = MainWithWorktree();

        RealPath.Resolve(GitWorktree.MainCheckout(worktree)!).Should().Be(RealPath.Resolve(main));
        GitWorktree.MainCheckout(main).Should().BeNull();
    }

    [Fact]
    public void A_submodule_style_git_file_without_commondir_is_not_a_worktree()
    {
        var folder = _git.Path("module");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ".git"), "gitdir: ../.git/modules/module");

        GitWorktree.MainCheckout(folder).Should().BeNull();
    }

    [Fact]
    public async Task A_worktree_without_skills_of_its_own_loads_the_main_checkouts_skills()
    {
        var (_, worktree) = MainWithWorktree();
        _home.RegisterProjectFolder(worktree);

        var scan = await SkillScanner.ScanAsync(_home.Paths);
        var project = scan.Sources.Single(s => s.Id == "project:" + worktree);

        EffectiveSetBuilder.Build(scan, project).Entries.Select(e => e.Skill.Name).Should().Equal("local");
    }

    [Fact]
    public async Task A_worktree_with_its_own_skills_loads_only_those()
    {
        var (_, worktree) = MainWithWorktree();
        GitSandbox.Write(worktree, ".claude/skills/own/SKILL.md", Doc("own"));
        _home.RegisterProjectFolder(worktree);

        var scan = await SkillScanner.ScanAsync(_home.Paths);
        var project = scan.Sources.Single(s => s.Id == "project:" + worktree);

        EffectiveSetBuilder.Build(scan, project).Entries.Select(e => e.Skill.Name).Should().Equal("own");
    }

    [Fact]
    public void Relative_gitdir_and_commondir_paths_are_followed()
    {
        var main = _git.Path("rel/main");
        var admin = Path.Combine(main, ".git", "worktrees", "w");
        Directory.CreateDirectory(admin);
        File.WriteAllText(Path.Combine(admin, "commondir"), "../..");
        var worktree = _git.Path("rel/w");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: ../main/.git/worktrees/w");

        GitWorktree.MainCheckout(worktree).Should().Be(Path.GetFullPath(main));
    }

    [Fact]
    public void A_worktree_of_a_bare_repository_has_no_main_checkout()
    {
        var admin = _git.Path("bare/repo.git/worktrees/w");
        Directory.CreateDirectory(admin);
        File.WriteAllText(Path.Combine(admin, "commondir"), "../..");
        var worktree = _git.Path("bare/w");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {admin}");

        GitWorktree.MainCheckout(worktree).Should().BeNull();
    }

    [Fact]
    public async Task A_worktree_whose_main_checkout_has_no_skills_loads_nothing_extra()
    {
        var main = _git.Init("plain");
        _git.Commit(main, "README.md", "x", "First");
        var worktree = _git.Path("plain-feature");
        _git.Git(main, "worktree", "add", "--quiet", worktree);
        _home.RegisterProjectFolder(worktree);

        var scan = await SkillScanner.ScanAsync(_home.Paths);

        EffectiveSetBuilder.Build(scan, scan.Sources.Single(s => s.Id == "project:" + worktree)).Entries.Should().BeEmpty();
    }
}
