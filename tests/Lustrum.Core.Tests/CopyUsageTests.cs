using FluentAssertions;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;
using Lustrum.Core.Usage;

namespace Lustrum.Core.Tests;

public class CopyUsageTests : IDisposable
{
    private static readonly DateTimeOffset Day = DateTimeOffset.Parse("2026-10-01T10:00:00Z");
    private readonly FakeClaudeHome _home = new();
    private readonly string _app;
    private readonly string _other;

    public CopyUsageTests()
    {
        _app = _home.Project("dev/app");
        _other = _home.Project("dev/other");
        _home.RegisterProjectFolder(_app);
        _home.RegisterProjectFolder(_other);
    }

    public void Dispose() => _home.Dispose();

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private static UsageReport Report(params SkillInvocation[] uses) =>
        UsageReport.Empty with
        {
            ByName = uses.GroupBy(u => u.Name).ToDictionary(g => g.Key, g => new SkillUsage(g.Key, g.Count(), g.Max(u => u.At), [], g.Count(), 0)),
            Invocations = uses,
        };

    private static SkillInvocation Use(string name, string? folder, int day = 0) => new(name, Day.AddDays(day), folder, InvocationKind.Model);

    [Fact]
    public async Task Each_use_of_a_shared_name_goes_to_the_copy_that_wins_where_it_ran()
    {
        _home.GlobalSkill("notes", Doc("notes"));
        _home.ProjectSkill(_other, "deploy", Doc("deploy"));
        _home.ProjectSkill(_app, "deploy", Doc("deploy"));
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        var report = Report(
            Use("deploy", _app), Use("deploy", Path.Combine(_app, "src"), 1), Use("deploy", _other, 2), Use("notes", _app));

        var usage = CopyUsage.Attribute(scan, report);

        var app = scan.Skills.Single(s => s.Name == "deploy" && s.Source.ProjectFolder == _app);
        var other = scan.Skills.Single(s => s.Name == "deploy" && s.Source.ProjectFolder == _other);
        usage.For(app)!.Count.Should().Be(2);
        usage.For(other)!.Count.Should().Be(1);
        usage.ForName(app)!.Count.Should().Be(3);
        usage.IsEstimate(app).Should().BeTrue();
        usage.IsEstimate(scan.Skills.Single(s => s.Name == "notes")).Should().BeFalse();
        usage.For(scan.Skills.Single(s => s.Name == "notes"))!.Count.Should().Be(1);
    }

    [Fact]
    public async Task A_personal_copy_takes_the_uses_in_a_project_it_shadows()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_app, "review", Doc("review"));
        var scan = await SkillScanner.ScanAsync(_home.Paths);

        var usage = CopyUsage.Attribute(scan, Report(Use("review", _app), Use("review", "/somewhere/else")));

        usage.For(scan.Skills.Single(s => s.Source.Kind == SourceKind.Global))!.Count.Should().Be(2);
        usage.For(scan.Skills.Single(s => s.Source.Kind == SourceKind.Project)).Should().BeNull();
    }

    [Fact]
    public async Task A_use_no_copy_can_be_named_for_is_kept_as_unattributed()
    {
        _home.ProjectSkill(_app, "deploy", Doc("deploy"));
        _home.ProjectSkill(_other, "deploy", Doc("deploy"));
        var scan = await SkillScanner.ScanAsync(_home.Paths);

        var usage = CopyUsage.Attribute(scan, Report(Use("deploy", "/not/a/project")));

        var copy = scan.Skills.First(s => s.Name == "deploy");
        usage.For(copy).Should().BeNull();
        usage.Unattributed(copy).Should().Be(1);
        usage.IsUnused(copy).Should().BeFalse();
    }

    [Fact]
    public async Task A_copy_turned_off_in_the_project_does_not_take_its_uses()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_app, "review", Doc("review"));
        _home.File(Path.Combine(_home.ClaudeDir, "settings.json"), """{ "skillOverrides": { "review": "off" } }""");
        var scan = await SkillScanner.ScanAsync(_home.Paths);

        var usage = CopyUsage.Attribute(scan, Report(Use("review", _app)));

        usage.Unattributed(scan.Skills.First(s => s.Name == "review")).Should().Be(1);
    }

    [Fact]
    public async Task The_signature_follows_the_copies_of_shared_names_only()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_app, "review", Doc("review"));
        var before = CopyUsage.Signature(await SkillScanner.ScanAsync(_home.Paths));
        _home.GlobalSkill("solo", Doc("solo"));
        var same = CopyUsage.Signature(await SkillScanner.ScanAsync(_home.Paths));
        _home.ProjectSkill(_other, "review", Doc("review"));
        var changed = CopyUsage.Signature(await SkillScanner.ScanAsync(_home.Paths));

        same.Should().Be(before);
        changed.Should().NotBe(before);
    }
}
