using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Lustrum.Core.Effective;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using CoreHealth = Lustrum.Core.Models.Health;

namespace Lustrum.App.ViewModels;

public sealed partial class SkillCardViewModel : ObservableObject
{
    public const int CompareTab = 2;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private bool _isBodyLoading = true;

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _usageText = "Reading Claude Code transcripts…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsageProjects))]
    private string _usageProjects = string.Empty;

    [ObservableProperty]
    private GitSection _git = GitSection.Hidden;

    public SkillCardViewModel(
        Skill skill,
        IReadOnlyList<Skill> sameNameElsewhere,
        EffectiveEntry? entry = null,
        string? projectName = null,
        MissingEntry? missing = null)
    {
        AvailabilityNote = missing is not null && projectName is not null
            ? $"Not loaded in {projectName}: {missing.Reason(SkillItemViewModel.SourceLabelFor) ?? $"it is stored in {SkillItemViewModel.SourceLabelFor(skill.Source)}"}"
            : NoteFor(entry, projectName);
        Skill = skill;
        Health = HealthPresentation.For(skill.Health);
        Title = skill.InvocationName;
        Description = string.IsNullOrWhiteSpace(skill.Description) ? "This skill has no description" : skill.Description;
        KindLabel = skill.Kind == SkillKind.Command ? "command" : "skill";
        KindIcon = skill.Kind == SkillKind.Command ? Symbol.SlashForward : Symbol.Sparkle;
        SourceIcon = SkillItemViewModel.SourceSymbol(skill.Source.Kind);
        SourceLabel = SkillItemViewModel.SourceLabelFor(skill.Source);
        FolderPath = System.IO.Path.GetDirectoryName(skill.Path)!;
        Frontmatter = skill.Frontmatter.Select(p => new FrontmatterEntry(p.Key, p.Value)).ToList();
        Conflicts = sameNameElsewhere
            .Select(s => new ConflictEntry(SkillItemViewModel.SourceLabelFor(s.Source), s.Path, s))
            .ToList();
        Comparison = sameNameElsewhere.Count > 0 ? new ComparisonViewModel(skill, sameNameElsewhere) : null;
        ModifiedText = skill.Modified == DateTimeOffset.MinValue ? "—" : skill.Modified.ToLocalTime().ToString("d MMM yyyy, HH:mm");
        SizeText = FormatSize(skill.SizeBytes);
    }

    public Skill Skill { get; }

    public HealthPresentation Health { get; }

    public string Title { get; }

    public string Description { get; }

    public bool IsCommand => Skill.Kind == SkillKind.Command;

    public string KindLabel { get; }

    public Symbol KindIcon { get; }

    public Symbol SourceIcon { get; }

    public string SourceLabel { get; }

    public string FilePath => Skill.Path;

    public string FolderPath { get; }

    public IReadOnlyList<FrontmatterEntry> Frontmatter { get; }

    public bool HasFrontmatter => Frontmatter.Count > 0;

    public IReadOnlyList<string> Files => Skill.Files;

    public bool HasFiles => Skill.Files.Count > 0;

    public IReadOnlyList<ConflictEntry> Conflicts { get; }

    public bool HasConflicts => Conflicts.Count > 0;

    public ComparisonViewModel? Comparison { get; }

    public bool ShowHealth => Skill.Health != CoreHealth.Ok;

    public bool HasUsageProjects => UsageProjects.Length > 0;

    public string? AvailabilityNote { get; }

    public bool HasAvailabilityNote => AvailabilityNote is not null;

    public string ModifiedText { get; }

    public GitCardState GitState { get; private set; } = GitCardState.Hidden;

    public SkillRepository? GitRepository => (GitState as ReadyGit)?.Report.Repository;

    public string SizeText { get; }

    [RelayCommand]
    private void CompareWith(ConflictEntry entry)
    {
        if (Comparison is null)
        {
            return;
        }

        var target = Comparison.Targets.First(t => t.Skill.Id == entry.Skill.Id);
        if (Comparison.Target == target)
        {
            Comparison.Reload();
        }
        else
        {
            Comparison.Target = target;
        }

        SelectedTab = CompareTab;
    }

    partial void OnSelectedTabChanged(int value)
    {
        if (value == CompareTab && Comparison is { Target: null, Targets.Count: > 0 })
        {
            Comparison.Target = Comparison.Targets[0];
        }
    }

    public void ApplyUsage(UsageState state)
    {
        UsageText = state.Describe(Skill);
        var usage = state.Report?.For(Skill.FullName);
        UsageProjects = usage is null
            ? string.Empty
            : string.Join(", ", usage.Projects.Select(p => System.IO.Path.GetFileName(p.TrimEnd('/', '\\'))).Distinct());
    }

    public void ApplyGit(GitCardState state, RepoState repo)
    {
        GitState = state;
        Git = GitSection.From(state, repo);
    }

    private static string? NoteFor(EffectiveEntry? entry, string? project)
    {
        if (entry is null || project is null)
        {
            return null;
        }

        if (entry.ShadowedBy is { } winner)
        {
            return $"Not loaded in {project}: the copy in {SkillItemViewModel.SourceLabelFor(winner.Source)} takes precedence";
        }

        return entry.Overrides.Count > 0
            ? $"Loaded in {project} instead of the copy in "
                + string.Join(", ", entry.Overrides.Select(o => SkillItemViewModel.SourceLabelFor(o.Source)).Distinct())
            : $"Loaded in {project}";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };
}

public sealed record ConflictEntry(string Source, string Path, Skill Skill);
