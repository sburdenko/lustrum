using System.Collections.Immutable;
using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed record UpdateIndex(
    ImmutableDictionary<string, SkillLocation> LocationBySkill,
    ImmutableDictionary<string, RepoState> States)
{
    public static UpdateIndex Empty { get; } = new(
        ImmutableDictionary<string, SkillLocation>.Empty,
        ImmutableDictionary<string, RepoState>.Empty.WithComparers(StringComparer.Ordinal));

    public bool HasChecked => States.Values.Any(s => s is CheckedRepo);

    public bool IsUpdating => States.Values.Any(s => s is UpdatingRepo);

    public RepoState StateOf(SkillRepository repository) => States.GetValueOrDefault(repository.Root, RepoState.NotChecked);

    /// <summary>
    /// Commits of the last fetched upstream that touch the skill's own folder or file, so a skill in a large
    /// repository is not reported as changed because something else in it moved.
    /// </summary>
    public int Incoming(Skill skill) =>
        LocationBySkill.TryGetValue(skill.Id, out var location) && StateOf(location.Repository) is CheckedRepo done
            ? done.IncomingFor(location)
            : 0;

    public IReadOnlyList<string> PathsIn(SkillRepository repository) =>
        LocationBySkill.Values.Where(l => l.Repository.Root == repository.Root).Select(l => l.RelativePath).Distinct(StringComparer.Ordinal).ToList();

    public UpdateIndex With(SkillRepository repository, RepoState state) => this with { States = States.SetItem(repository.Root, state) };

    public UpdateIndex Mapping(Skill skill, SkillLocation location) =>
        this with { LocationBySkill = LocationBySkill.SetItem(skill.Id, location) };
}
