using FluentAssertions;
using Lustrum.Core.Linting;
using Lustrum.Core.Problems;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ReferenceLinterTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _folder;

    public ReferenceLinterTests()
    {
        _folder = _home.Dir("skill");
        _home.File(Path.Combine(_folder, "scripts", "run.sh"), "echo");
        _home.File(Path.Combine(_folder, "references", "api guide.md"), "x");
    }

    public void Dispose() => _home.Dispose();

    private IReadOnlyList<string> Missing(string body) => ReferenceLinter.Lint(body, _folder).Select(f => f.Subject).ToList();

    [Fact]
    public void Markdown_links_to_missing_files_are_reported_and_present_ones_are_not()
    {
        Missing("See [the script](scripts/run.sh), [notes](references/notes.md) and [guide](./references/api%20guide.md#intro).")
            .Should().Equal("references/notes.md");
    }

    [Fact]
    public void Encoded_spaces_in_links_are_decoded_before_the_file_is_looked_up()
    {
        Missing("[guide](references/api%20guide.md) and [gone](references/old%20guide.md) and [pct](references/100%.md)")
            .Should().Equal("references/old guide.md", "references/100%.md");
    }

    [Fact]
    public void Inline_paths_count_under_spec_folders_and_under_resource_folders_the_skill_has()
    {
        _home.File(Path.Combine(_folder, "templates", "a.md"), "x");

        Missing("Run `scripts/missing.sh`, edit `config.json` or `src/app.ts`, fill `templates/b.md`, update `docs/architecture.md`.")
            .Should().Equal("scripts/missing.sh", "templates/b.md");
    }

    [Fact]
    public void Reference_style_links_and_links_with_parentheses_are_checked()
    {
        Missing("See [guide][g] and [old](references/a_(1).md).\n\n[g]: references/gone.md \"Guide\"\n")
            .Should().Equal("references/a_(1).md", "references/gone.md");
    }

    [Fact]
    public void Backslashes_are_read_as_folder_separators() =>
        Missing("[run](scripts\\run.sh) and [gone](scripts\\gone.sh)").Should().Equal("scripts/gone.sh");

    [Theory]
    [InlineData("Use `[text](path)` syntax.")]
    [InlineData("[nul](a%00b.md)")]
    [InlineData("`references/[topic].md`")]
    [InlineData("[call](tel:+123)")]
    [InlineData("[img](data:image/png;base64,xyz)")]
    public void Link_syntax_in_code_odd_characters_and_other_schemes_are_not_checked(string body) =>
        Missing(body).Should().BeEmpty();

    [Theory]
    [InlineData("[site](https://example.com/a.md)")]
    [InlineData("[top](#usage)")]
    [InlineData("[abs](/etc/hosts)")]
    [InlineData("[home](~/notes.md)")]
    [InlineData("[out](../other/SKILL.md)")]
    [InlineData("[tpl](templates/{name}.md)")]
    [InlineData("[mail](mailto:a@b.c)")]
    public void Urls_anchors_absolute_paths_placeholders_and_escapes_are_not_checked(string body) =>
        Missing(body).Should().BeEmpty();

    [Fact]
    public void Paths_inside_fenced_code_are_not_checked()
    {
        Missing("```bash\nbash scripts/missing.sh\n[x](references/gone.md)\n```\n").Should().BeEmpty();
    }

    [Fact]
    public void A_fence_closes_only_on_its_own_marker_and_length()
    {
        Missing("````md\n```\n[a](references/a.md)\n```\n~~~\n````\n[b](references/b.md)\n").Should().Equal("references/b.md");
        Missing("Inline ```code``` here.\n[c](references/c.md)\n").Should().Equal("references/c.md");
    }

    [Fact]
    public void A_long_line_of_brackets_is_handled_quickly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        Missing(new string('[', 64 * 1024)).Should().BeEmpty();

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void A_missing_file_is_reported_once()
    {
        Missing("[a](scripts/gone.sh) and again [b](scripts/gone.sh)").Should().Equal("scripts/gone.sh");
    }
}

public class ToolLinterTests
{
    private static IReadOnlyList<(FindingKind Kind, string Message)> Lint(params string[] entries) =>
        ToolLinter.Lint(entries).Select(f => (f.Kind, f.Message)).ToList();

    [Theory]
    [InlineData("Bash(git:*)")]
    [InlineData("Read")]
    [InlineData("WebFetch(domain:example.com)")]
    [InlineData("mcp__github__create_issue")]
    public void Valid_entries_pass(string entry) => Lint(entry).Should().BeEmpty();

    [Theory]
    [InlineData("Bash")]
    [InlineData("Bash(*)")]
    [InlineData("Bash(*:*)")]
    [InlineData("PowerShell")]
    public void A_shell_without_a_command_pattern_is_broad(string entry) =>
        Lint(entry).Should().ContainSingle().Which.Kind.Should().Be(FindingKind.BroadTool);

    [Theory]
    [InlineData("", "allowed-tools has an empty entry")]
    [InlineData("Read(", "allowed-tools entry Read( has unbalanced parentheses")]
    [InlineData("Bash(ls) x", "allowed-tools entry Bash(ls) x has unbalanced parentheses")]
    [InlineData("Bash()", "allowed-tools entry Bash() has an empty pattern")]
    [InlineData("Web Fetch", "allowed-tools entry Web Fetch has no valid tool name")]
    public void Malformed_entries_are_syntax_findings(string entry, string message) =>
        Lint(entry).Should().Equal((FindingKind.ToolSyntax, message));

    [Fact]
    public void A_name_close_to_a_known_tool_is_reported_as_a_misspelling()
    {
        Lint("Bsh(ls)").Should().Equal((FindingKind.UnknownTool, "allowed-tools names Bsh, which is not a Claude Code tool; did you mean Bash?"));
        Lint("read").Single().Message.Should().EndWith("did you mean Read?");
    }

    [Theory]
    [InlineData("Teleport")]
    [InlineData("TaskCreate")]
    [InlineData("Xy")]
    public void Names_far_from_every_known_tool_pass_because_the_list_can_be_behind(string entry) =>
        Lint(entry).Should().BeEmpty();
}

public class LintIntegrationTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task Findings_are_collected_while_scanning_and_listed_as_problems()
    {
        _home.GlobalSkill("tool", "---\nname: tool\ndescription: d\nallowed-tools: Bash Grpe\n---\nSee [the guide](references/guide.md).\n");
        _home.GlobalSkill("ok", "---\nname: ok\ndescription: d\nallowed-tools: Read\n---\nNothing linked.\n");

        var scan = await SkillScanner.ScanAsync(_home.Paths);
        var problems = ProblemCollector.Collect(scan);

        scan.Skills.Single(s => s.Name == "tool").Findings.Select(f => f.Kind)
            .Should().Equal(FindingKind.MissingFile, FindingKind.BroadTool, FindingKind.UnknownTool);
        problems.Select(p => p.Kind).Should().BeEquivalentTo([ProblemKind.MissingReference, ProblemKind.BroadPermission, ProblemKind.ToolEntry]);
        scan.Skills.Single(s => s.Name == "ok").Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Commands_are_checked_for_tools_but_not_for_links()
    {
        _home.GlobalCommand("ship", "---\nallowed-tools: Bash\n---\nRun `scripts/ship.sh`, see [notes](notes.md).\n");

        var scan = await SkillScanner.ScanAsync(_home.Paths);

        scan.Skills.Single().Findings.Select(f => f.Kind).Should().Equal(FindingKind.BroadTool);
    }

    [Fact]
    public async Task A_skill_with_broken_frontmatter_is_not_linted()
    {
        _home.GlobalSkill("bad", "---\nname: [oops\n---\n[x](gone.md)\n");

        var scan = await SkillScanner.ScanAsync(_home.Paths);

        scan.Skills.Single().Findings.Should().BeEmpty();
    }
}
