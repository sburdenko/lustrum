using System.Collections.Immutable;
using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed record UpdateIndex(
    ImmutableDictionary<string, SkillRepository> RepositoryBySkill,
    ImmutableDictionary<string, RepoState> States)
{
    public static UpdateIndex Empty { get; } = new(
        ImmutableDictionary<string, SkillRepository>.Empty,
        ImmutableDictionary<string, RepoState>.Empty.WithComparers(StringComparer.Ordinal));

    public bool HasChecked => States.Values.Any(s => s is CheckedRepo);

    public bool IsUpdating => States.Values.Any(s => s is UpdatingRepo);

    public RepoState StateOf(SkillRepository repository) => States.GetValueOrDefault(repository.Root, RepoState.NotChecked);

    /// <summary>
    /// Commits a skill could pull. Only skills repositories count: a project repository being behind says nothing
    /// about the skill itself, so its count stays on the card.
    /// </summary>
    public int Behind(Skill skill) =>
        RepositoryBySkill.TryGetValue(skill.Id, out var repository)
        && repository.Role == RepositoryRole.SkillsOnly
        && StateOf(repository) is CheckedRepo done
            ? done.Tracking.Behind
            : 0;

    public UpdateIndex With(SkillRepository repository, RepoState state) => this with { States = States.SetItem(repository.Root, state) };

    public UpdateIndex Mapping(Skill skill, SkillRepository repository) =>
        this with { RepositoryBySkill = RepositoryBySkill.SetItem(skill.Id, repository) };
}
