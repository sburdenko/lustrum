using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.Screenshots;

/// <summary>Runs real git against the demo home while the window shows the masked /home/dev paths.</summary>
internal sealed class DemoGit(ISkillGit inner, string home, string shownHome) : ISkillGit
{
    private readonly string _realHome = RealPath.Resolve(home);

    public SkillLocation? Locate(Skill skill) => inner.Locate(Unmask(skill)) is { } location ? Mask(location) : null;

    public async Task<SkillGitReport?> InspectAsync(Skill skill, CancellationToken cancellationToken) =>
        await inner.InspectAsync(Unmask(skill), cancellationToken) is { } report ? report with { Location = Mask(report.Location) } : null;

    public Task<GitTracking> CheckForUpdatesAsync(SkillRepository repository, CancellationToken cancellationToken) =>
        inner.CheckForUpdatesAsync(Unmask(repository), cancellationToken);

    public Task<GitTracking> UpdateAsync(SkillRepository repository, CancellationToken cancellationToken) =>
        inner.UpdateAsync(Unmask(repository), cancellationToken);

    private Skill Unmask(Skill skill) => skill with
    {
        Path = skill.Path.Replace(shownHome, home),
        Source = skill.Source with { RootPath = skill.Source.RootPath.Replace(shownHome, home) },
    };

    private SkillRepository Unmask(SkillRepository repository) => repository with { Root = repository.Root.Replace(shownHome, _realHome) };

    private SkillLocation Mask(SkillLocation location) =>
        location with { Repository = location.Repository with { Root = location.Repository.Root.Replace(_realHome, shownHome) } };
}
