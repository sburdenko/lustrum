using FluentAssertions;
using Lustrum.Core.Git;

namespace Lustrum.Core.Tests;

public class UpdatePolicyTests
{
    private static readonly SkillRepository SkillsRepo = new("/home/u/.claude/skills", RepositoryRole.SkillsOnly);
    private static readonly SkillRepository ProjectRepo = new("/dev/storefront", RepositoryRole.Containing);
    private static readonly GitChange Dirty = new("a/SKILL.md", GitChangeKind.Modified);

    private static GitTracking Tracking(int ahead = 0, int behind = 0) => new("main", "origin/main", ahead, behind);

    [Fact]
    public void A_clean_skills_repository_behind_its_upstream_can_be_updated() =>
        UpdatePolicy.Evaluate(SkillsRepo, Tracking(behind: 3), []).Should().Be(new UpdateAvailable(3));

    [Fact]
    public void Nothing_to_pull_is_up_to_date_even_with_local_changes() =>
        UpdatePolicy.Evaluate(SkillsRepo, Tracking(ahead: 1), [Dirty]).Should().BeOfType<UpToDate>();

    [Fact]
    public void A_repository_holding_more_than_skills_is_never_updated() =>
        UpdatePolicy.Evaluate(ProjectRepo, Tracking(behind: 2), []).Should()
            .Be(new UpdateBlocked("This skill is part of the storefront repository; update it there with git"));

    [Theory]
    [InlineData(null, "origin/main", 0, 1, false, "HEAD is detached, so there is no branch to update")]
    [InlineData("main", null, 0, 0, false, "Branch main has no upstream to update from")]
    [InlineData("main", "origin/main", 0, 2, true, "The repository has uncommitted changes")]
    [InlineData("main", "origin/main", 1, 2, false, "Branch main has 1 local commit that origin/main does not have")]
    [InlineData("main", "origin/main", 4, 2, false, "Branch main has 4 local commits that origin/main does not have")]
    public void Blocks_updates_that_could_lose_or_merge_work(string? branch, string? upstream, int ahead, int behind, bool dirty, string reason)
    {
        var tracking = new GitTracking(branch, upstream, ahead, behind);

        var decision = UpdatePolicy.Evaluate(SkillsRepo, tracking, dirty ? [Dirty] : []);

        decision.Should().Be(new UpdateBlocked(reason));
        decision.CanUpdate.Should().BeFalse();
    }
}
