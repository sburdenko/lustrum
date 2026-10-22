using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class CopyUsageViewModelTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-01T10:00:00Z");

    private static async Task<MainViewModel> Scanned(UsageReport usage)
    {
        var vm = new MainViewModel(
            (_, _) => Task.FromResult(TestData.WithShadowing()),
            _ => Task.FromResult(usage),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme(), new FakeSkillGit(), new FakeSkillWriter()));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static UsageReport Uses(params SkillInvocation[] uses) => UsageReport.Empty with
    {
        TranscriptCount = 1,
        ByName = uses.GroupBy(u => u.Name).ToDictionary(g => g.Key, g => new SkillUsage(g.Key, g.Count(), g.Max(u => u.At), [], g.Count(), 0, g.Select(u => u.At).ToList())),
        Invocations = uses,
    };

    [Fact]
    public async Task Uses_of_a_shared_name_go_to_the_copy_that_wins_and_the_card_says_it_is_an_estimate()
    {
        var vm = await Scanned(Uses(
            new SkillInvocation("design", At, "/dev/app", InvocationKind.Model),
            new SkillInvocation("design", At, null, InvocationKind.User)));

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == "design" && s.Skill.Source.Kind == SourceKind.Global);
        vm.Card!.UsageText.Should().StartWith("About 2 of the 2 uses of design ran this copy").And.EndWith("an estimate from the folders the sessions ran in");
        vm.Card.UsageStats[0].Value.Should().Be("2");

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == "design" && s.Skill.Source.Kind == SourceKind.Project);
        vm.Card!.UsageText.Should().Be("None of the 2 uses of design ran this copy; an estimate from the folders the sessions ran in");
    }

    [Fact]
    public async Task The_unused_chip_keeps_a_copy_that_never_ran()
    {
        var vm = await Scanned(Uses(new SkillInvocation("design", At, null, InvocationKind.Model), new SkillInvocation("notes", At, null, InvocationKind.Model)));

        vm.Chips.Single(c => c.Label == "unused").IsOn = true;

        vm.VisibleSkills.Select(s => (s.Name, s.Skill.Source.Kind)).Should().BeEquivalentTo([("design", SourceKind.Project), ("elsewhere", SourceKind.Project), ("ui", SourceKind.Plugin)]);
    }
}
