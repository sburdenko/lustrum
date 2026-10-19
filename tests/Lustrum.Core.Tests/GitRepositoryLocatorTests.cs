using FluentAssertions;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class GitRepositoryLocatorTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    private SkillSource Global => new("global", SourceKind.Global, "Global", _home.ClaudeDir, null, true);

    private static SkillSource ProjectSource(string projectDir) =>
        new($"project:{projectDir}", SourceKind.Project, "app", Path.Combine(projectDir, ".claude"), null, true);

    private static void MarkRepository(string folder) => Directory.CreateDirectory(Path.Combine(folder, ".git"));

    public void Dispose() => _home.Dispose();

    [Fact]
    public void A_skill_folder_that_is_its_own_repository_is_skills_only()
    {
        var file = _home.GlobalSkill("tool", "x");
        MarkRepository(Path.GetDirectoryName(file)!);

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, file))!;

        location.Repository.Role.Should().Be(RepositoryRole.SkillsOnly);
        location.Repository.Root.Should().Be(RealPath.Resolve(Path.GetDirectoryName(file)!));
        location.RelativePath.Should().Be(".");
    }

    [Fact]
    public void The_whole_skills_folder_as_a_repository_is_skills_only()
    {
        var file = _home.GlobalSkill("tool", "x");
        MarkRepository(Path.Combine(_home.ClaudeDir, "skills"));

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, file))!;

        location.Repository.Role.Should().Be(RepositoryRole.SkillsOnly);
        location.RelativePath.Should().Be("tool");
    }

    [Fact]
    public void A_commands_repository_is_skills_only_and_points_at_the_command_file()
    {
        var file = _home.GlobalCommand("ship", "x");
        File.WriteAllText(Path.Combine(_home.ClaudeDir, "commands", ".git"), "gitdir: /elsewhere");

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, file, SkillKind.Command))!;

        location.Repository.Role.Should().Be(RepositoryRole.SkillsOnly);
        location.RelativePath.Should().Be("ship.md");
    }

    [Fact]
    public void A_project_repository_contains_the_skill()
    {
        var project = _home.Project("dev/storefront");
        var file = _home.ProjectSkill(project, "deploy", "x");
        MarkRepository(project);

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(ProjectSource(project), file))!;

        location.Repository.Role.Should().Be(RepositoryRole.Containing);
        location.Repository.Name.Should().Be("storefront");
        location.RelativePath.Should().Be(".claude/skills/deploy");
    }

    [Fact]
    public void A_repository_around_the_claude_folder_contains_the_skill()
    {
        var file = _home.GlobalSkill("tool", "x");
        MarkRepository(_home.ClaudeDir);

        GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, file))!.Repository.Role.Should().Be(RepositoryRole.Containing);
    }

    [Fact]
    public void A_skill_outside_any_repository_has_no_location()
    {
        var file = _home.GlobalSkill("tool", "x");

        GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, file)).Should().BeNull();
    }

    [Theory]
    [InlineData(SourceKind.Plugin)]
    [InlineData(SourceKind.Synced)]
    public void Plugin_and_synced_skills_are_left_to_their_managers(SourceKind kind)
    {
        var file = _home.GlobalSkill("tool", "x");
        MarkRepository(Path.GetDirectoryName(file)!);
        var source = Global with { Kind = kind };

        GitRepositoryLocator.Applies(GitSandbox.SkillAt(source, file)).Should().BeFalse();
        GitRepositoryLocator.Locate(GitSandbox.SkillAt(source, file)).Should().BeNull();
    }

    [Fact]
    public void A_skills_folder_linked_into_a_repository_resolves_to_that_repository()
    {
        var dotfiles = _home.Dir("dotfiles");
        MarkRepository(dotfiles);
        var real = Path.Combine(dotfiles, "skills");
        _home.File(Path.Combine(real, "tool", "SKILL.md"), "x");
        Directory.CreateSymbolicLink(Path.Combine(_home.ClaudeDir, "skills"), real);

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, Path.Combine(_home.ClaudeDir, "skills", "tool", "SKILL.md")))!;

        location.Repository.Root.Should().Be(RealPath.Resolve(dotfiles));
        location.Repository.Role.Should().Be(RepositoryRole.Containing);
        location.RelativePath.Should().Be("skills/tool");
    }

    [Fact]
    public void A_skill_linked_to_its_own_repository_is_skills_only()
    {
        var repo = _home.Dir("dev/tool");
        MarkRepository(repo);
        _home.File(Path.Combine(repo, "SKILL.md"), "x");
        Directory.CreateDirectory(Path.Combine(_home.ClaudeDir, "skills"));
        Directory.CreateSymbolicLink(Path.Combine(_home.ClaudeDir, "skills", "tool"), repo);

        var location = GitRepositoryLocator.Locate(GitSandbox.SkillAt(Global, Path.Combine(_home.ClaudeDir, "skills", "tool", "SKILL.md")))!;

        location.Repository.Role.Should().Be(RepositoryRole.SkillsOnly);
        location.RelativePath.Should().Be(".");
    }
}
