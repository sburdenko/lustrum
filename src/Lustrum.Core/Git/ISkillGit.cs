using Lustrum.Core.Models;

namespace Lustrum.Core.Git;

public interface ISkillGit
{
    SkillLocation? Locate(Skill skill);

    /// <summary>Reads local state only; nothing goes over the network.</summary>
    Task<SkillGitReport?> InspectAsync(Skill skill, CancellationToken cancellationToken);

    /// <summary>Fetches the upstream, when there is one, and returns the fresh tracking state.</summary>
    Task<GitTracking> CheckForUpdatesAsync(SkillRepository repository, CancellationToken cancellationToken);

    /// <summary>
    /// For each skill folder (or command file) in the repository, how many commits of the fetched upstream touch it.
    /// Local only; call it after <see cref="CheckForUpdatesAsync"/>.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> IncomingCommitsAsync(SkillRepository repository, IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken);

    /// <summary>
    /// Fast-forwards a skills-only repository to its upstream after re-checking the update policy.
    /// Throws <see cref="GitUpdateRefusedException"/> when the policy does not allow it.
    /// </summary>
    Task<GitTracking> UpdateAsync(SkillRepository repository, CancellationToken cancellationToken);
}
