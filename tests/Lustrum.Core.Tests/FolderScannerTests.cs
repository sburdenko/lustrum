using FluentAssertions;
using Lustrum.Core.Models;
using Lustrum.Core.Problems;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class FolderScannerTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _repo;

    public FolderScannerTests() => _repo = _home.Dir("repo");

    public void Dispose() => _home.Dispose();

    private void Write(string relative, string content) => _home.File(Path.Combine(_repo, relative.Replace('/', Path.DirectorySeparatorChar)), content);

    private static string Skill(string name) => $"---\nname: {name}\ndescription: {name} skill\n---\nBody\n";

    private ScanResult Scan() => FolderScanner.Scan(_repo);

    [Fact]
    public void Claude_folders_at_any_depth_plugins_and_skill_collections_are_read()
    {
        Write(".claude/skills/deploy/SKILL.md", Skill("deploy"));
        Write(".claude/commands/ship.md", "Ship it.\n");
        Write("apps/web/.claude/skills/web/SKILL.md", Skill("web"));
        Write("plugins/kit/.claude-plugin/plugin.json", "{}");
        Write("plugins/kit/skills/lint/SKILL.md", Skill("lint"));
        Write("plugins/kit/commands/fix.md", "Fix it.\n");
        Write("catalog/skills/pdf/SKILL.md", Skill("pdf"));

        var scan = Scan();

        scan.Skills.Select(s => s.InvocationName).Should().BeEquivalentTo("deploy", "/ship", "web", "lint", "/fix", "pdf");
        scan.Sources.Select(s => s.DisplayName).Should().BeEquivalentTo(".claude", "apps/web/.claude", "plugins/kit", "catalog");
    }

    [Fact]
    public void A_commands_folder_outside_claude_folders_and_plugins_is_documentation_not_commands()
    {
        Write("docs/commands/install.md", "# install\n");
        Write("commands/build.md", "# build\n");

        Scan().Skills.Should().BeEmpty();
    }

    [Fact]
    public void Claude_folders_are_found_at_any_depth()
    {
        Write("a/b/c/d/e/f/.claude/skills/deep/SKILL.md", Skill("deep"));

        Scan().Skills.Select(s => s.Name).Should().Equal("deep");
    }

    [Fact]
    public void A_folder_of_skill_folders_is_read_as_a_collection()
    {
        Write("one/SKILL.md", Skill("one"));
        Write("two/SKILL.md", Skill("two"));

        Scan().Skills.Select(s => s.Name).Should().BeEquivalentTo("one", "two");
    }

    [Fact]
    public void A_trailing_separator_on_the_folder_is_ignored()
    {
        Write(".claude/skills/deploy/SKILL.md", Skill("deploy"));

        FolderScanner.Scan(_repo + Path.DirectorySeparatorChar).Sources.Single().DisplayName.Should().Be(".claude");
    }

    [Fact]
    public void A_folder_that_is_one_skill_is_read_as_that_skill()
    {
        Write("SKILL.md", Skill("solo"));
        Write("scripts/run.sh", "echo");

        Scan().Skills.Select(s => s.Name).Should().Equal("solo");
    }

    [Fact]
    public void Excluded_and_hidden_folders_are_skipped()
    {
        Write("node_modules/pkg/.claude/skills/dep/SKILL.md", Skill("dep"));
        Write(".cache/.claude/skills/hidden/SKILL.md", Skill("hidden"));

        Scan().Skills.Should().BeEmpty();
    }

    [Fact]
    public void Problems_in_the_folder_are_collected_without_reading_the_home()
    {
        _home.GlobalSkill("mine", "---\nname: [oops\n---\n");
        Write(".claude/skills/broken/SKILL.md", "---\nname: [oops\n---\n");
        Write(".claude/skills/linked/SKILL.md", Skill("linked") + "See [guide](references/guide.md).\n");

        var problems = ProblemCollector.Collect(Scan());

        problems.Select(p => (p.Kind, p.Subject)).Should().BeEquivalentTo([(ProblemKind.BrokenSkill, "broken"), (ProblemKind.MissingReference, "linked")]);
    }
}
