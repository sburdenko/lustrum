using FluentAssertions;
using Lustrum.App.ViewModels;
using Lustrum.Core.Indexing;

namespace Lustrum.App.Tests;

public class NavTreeBuilderTests
{
    [Fact]
    public void Top_level_groups_come_in_fixed_order_with_counts()
    {
        var roots = NavTreeBuilder.Build(TestData.Default());

        roots.Select(n => n.Title).Should().Equal("All", "Global", "Projects", "Plugins", "Problems");
        roots.Select(n => n.Count).Should().Equal(4, 2, 1, 1, 2);
    }

    [Fact]
    public void Empty_and_ghost_projects_are_hidden_by_default()
    {
        var projects = NavTreeBuilder.Build(TestData.Default()).Single(n => n.Title == "Projects");

        projects.Children.Select(c => c.Title).Should().Equal("app");
    }

    [Fact]
    public void Empty_and_ghost_projects_can_be_included_and_are_dimmed()
    {
        var projects = NavTreeBuilder.Build(TestData.Default(), includeEmptySources: true)
            .Single(n => n.Title == "Projects");

        projects.Children.Select(c => c.Title).Should().Equal("app", "empty", "gone");
        projects.Children.Skip(1).Should().OnlyContain(c => c.IsDimmed);
        projects.Children.First().IsDimmed.Should().BeFalse();
    }

    [Fact]
    public void Plugins_are_grouped_by_marketplace()
    {
        var plugins = NavTreeBuilder.Build(TestData.Default()).Single(n => n.Title == "Plugins");

        var marketplace = plugins.Children.Should().ContainSingle().Subject;
        marketplace.Title.Should().Be("market");
        marketplace.Scope.Should().Be(new MarketplaceScope("market"));
        marketplace.Children.Should().ContainSingle().Which.Title.Should().Be("unity");
    }

    [Fact]
    public void Problems_counts_broken_skills_and_missing_projects()
    {
        var problems = NavTreeBuilder.Build(TestData.Default()).Single(n => n.Title == "Problems");

        problems.Scope.Should().Be(SkillScope.Problems);
        problems.Count.Should().Be(2);
    }

    [Fact]
    public void Synced_sources_get_their_own_group_only_when_present()
    {
        var scan = TestData.Scan(
            [TestData.Global, TestData.Account, TestData.SyncedPlugin],
            [TestData.Skill(TestData.Account, "xlsx"), TestData.Skill(TestData.SyncedPlugin, "critique")]);

        var roots = NavTreeBuilder.Build(scan);

        roots.Select(n => n.Title).Should().Equal("All", "Global", "Projects", "Plugins", "claude.ai", "Problems");
        var synced = roots.Single(n => n.Title == "claude.ai");
        synced.Count.Should().Be(2);
        synced.Children.Select(c => c.Title).Should().Equal("anthropic-skills", "design");
        NavTreeBuilder.Build(TestData.Default()).Should().NotContain(n => n.Title == "claude.ai");
    }
}
