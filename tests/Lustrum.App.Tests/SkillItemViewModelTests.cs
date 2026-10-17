using FluentAssertions;
using Lustrum.App.ViewModels;

namespace Lustrum.App.Tests;

public class SkillItemViewModelTests
{
    [Fact]
    public void Synced_skill_shows_its_namespace_as_prefix_and_source()
    {
        var item = new SkillItemViewModel(TestData.Skill(TestData.Account, "xlsx"));

        item.Prefix.Should().Be("anthropic-skills:");
        item.SourceLabel.Should().Be("anthropic-skills");
        item.SourceIcon.Should().Be(FluentIcons.Common.Symbol.Cloud);
    }

    [Fact]
    public void Plugin_skill_source_label_is_the_plugin_name()
    {
        new SkillItemViewModel(TestData.Skill(TestData.Plugin, "ui")).SourceLabel.Should().Be("unity");
    }
}
