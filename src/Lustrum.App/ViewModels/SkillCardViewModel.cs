using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Lustrum.Core.ClaudeSettings;
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

    [ObservableProperty]
    private IReadOnlyList<UsageStat> _usageStats = EmptyStats;

    [ObservableProperty]
    private IReadOnlyList<UsageDay> _usageDays = EmptyDays;

    public const int UsageWindowDays = 30;

    private static readonly IReadOnlyList<UsageStat> EmptyStats = Stats(null);

    private static readonly IReadOnlyList<UsageDay> EmptyDays =
        Enumerable.Range(0, UsageWindowDays).Select(i => new UsageDay(false, i == UsageWindowDays - 1)).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSetVisibility), nameof(VisibilityHint), nameof(VisibilityNote), nameof(HasVisibilityNote))]
    private VisibilityPanel? _visibility;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditVisibility))]
    private bool _isSavingVisibility;

    [ObservableProperty]
    private VisibilityOption? _selectedVisibility;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCopyTargets))]
    private IReadOnlyList<CopyTarget> _copyTargets = [];

    private bool _showingVisibility;

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

    public int Number { get; init; }

    public string? Issue { get; init; }

    public ContextPanel? Context { get; init; }

    public IReadOnlyList<CheckRow> Checks { get; init; } = [];

    public bool HasChecks => Checks.Count > 0;

    public bool HasIssue => Issue is not null;

    public string HeaderLine
    {
        get
        {
            var kind = Skill.Kind == SkillKind.Command ? "COMMAND" : "SKILL";
            var source = Skill.Source.Kind switch
            {
                SourceKind.Managed => "MANAGED",
                SourceKind.Global => "GLOBAL",
                SourceKind.Synced => $"CLAUDE.AI · {SourceLabel.ToUpperInvariant()}",
                _ => SourceLabel.ToUpperInvariant(),
            };
            var prefix = Skill.FullName.Length > Skill.Name.Length && Skill.Kind == SkillKind.Skill
                ? $" / {Skill.FullName[..^Skill.Name.Length]}"
                : string.Empty;
            return $"{Number:000} / {kind} / {source}{prefix}";
        }
    }

    public string HealthBadge => Skill.Health switch
    {
        CoreHealth.Ok => "OK",
        CoreHealth.DescriptionTooLong => "TOO LONG",
        CoreHealth.ParseError => "PARSE ERROR",
        CoreHealth.NoFrontmatter => "NO FRONTMATTER",
        CoreHealth.MissingName => "NO NAME",
        CoreHealth.MissingDescription => "NO DESCRIPTION",
        CoreHealth.Orphaned => "SOURCE GONE",
        _ => Skill.Health.ToString().ToUpperInvariant(),
    };

    public double TitleSize => Title.Length switch
    {
        > 15 => 46,
        > 11 => 58,
        _ => 72,
    };

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

    public string ConflictsTitle => Conflicts.Count == 1 ? "SAME NAME · 1 OTHER COPY" : $"SAME NAME · {Conflicts.Count} OTHER COPIES";

    public ComparisonViewModel? Comparison { get; }

    public bool ShowHealth => Skill.Health != CoreHealth.Ok;

    public bool HasUsageProjects => UsageProjects.Length > 0;

    public string? AvailabilityNote { get; }

    public bool HasAvailabilityNote => AvailabilityNote is not null;

    public string ModifiedText { get; }

    public event Action<SkillCardViewModel, SkillVisibility>? VisibilityRequested;

    public IReadOnlyList<VisibilityOption> VisibilityOptions => VisibilityOption.All;

    public bool CanSetVisibility => Visibility is not null;

    public bool CanEditVisibility => !IsSavingVisibility;

    public string? VisibilityHint => Visibility?.Hint;

    public string? VisibilityNote => Visibility?.Note;

    public bool HasVisibilityNote => VisibilityNote is not null;

    public bool HasCopyTargets => CopyTargets.Count > 0;

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

    public void ApplyUsage(UsageState state) => ApplyUsage(state, DateOnly.FromDateTime(DateTime.Now), TimeZoneInfo.Local);

    public void ApplyUsage(UsageState state, DateOnly today, TimeZoneInfo zone)
    {
        UsageText = state.Describe(Skill);
        var usage = state.Report?.For(Skill.FullName);
        UsageStats = Stats(usage);
        UsageDays = usage is null
            ? EmptyDays
            : usage.PerDay(today, UsageWindowDays, zone).Select((n, i) => new UsageDay(n > 0, i == UsageWindowDays - 1)).ToList();
        UsageProjects = usage is null
            ? string.Empty
            : string.Join(", ", usage.Projects.Select(p => System.IO.Path.GetFileName(p.TrimEnd('/', '\\'))).Distinct());
    }

    public void ShowVisibility(VisibilityPanel? panel)
    {
        _showingVisibility = true;
        try
        {
            Visibility = panel;
            SelectedVisibility = panel is null ? null : VisibilityOption.For(panel.Current);
        }
        finally
        {
            _showingVisibility = false;
        }
    }

    partial void OnSelectedVisibilityChanged(VisibilityOption? value)
    {
        if (!_showingVisibility && !IsSavingVisibility && value is not null && Visibility is { } panel && value.Value != panel.Current)
        {
            VisibilityRequested?.Invoke(this, value.Value);
        }
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

        if (entry.Availability == Availability.OnDemand)
        {
            return $"Loads in {project} once Claude works on files under {entry.LoadsUnder}";
        }

        var loaded = entry.VisibilityValue switch
        {
            SkillVisibility.Off => $"Turned off in {project}: hidden from Claude and from the / menu",
            SkillVisibility.On => $"Loaded in {project}",
            var other => $"Loaded in {project} as {VisibilityOption.For(other).Label.ToLowerInvariant()}",
        };
        return entry.Overrides.Count > 0
            ? $"{loaded}, ahead of " + string.Join(", ", entry.Overrides.Select(o => CopyName(o)).Distinct())
            : loaded;
    }

    private static string CopyName(Skill skill) => skill switch
    {
        { Source.Kind: SourceKind.Project } => "the project's own copy",
        { Source.Kind: SourceKind.Managed } => "the managed copy",
        { Kind: SkillKind.Command } => $"the command in {SkillItemViewModel.SourceLabelFor(skill.Source)}",
        _ => $"the copy in {SkillItemViewModel.SourceLabelFor(skill.Source)}",
    };

    private static IReadOnlyList<UsageStat> Stats(Lustrum.Core.Usage.SkillUsage? usage) =>
    [
        new("USES", usage?.Count.ToString() ?? "0", usage is not null, false),
        new("BY CLAUDE", usage?.ByModel.ToString() ?? "0", usage is not null, false),
        new("BY YOU", usage?.ByUser.ToString() ?? "0", usage is not null, false),
        new("LAST", usage is null ? "—" : LastText(usage.LastUsed.ToLocalTime()), usage is not null, true),
    ];

    private static string LastText(DateTimeOffset last) =>
        last.Year == DateTimeOffset.Now.Year ? last.ToString("dd.MM") : last.ToString("MM.yy");

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };
}

public sealed record ConflictEntry(string Source, string Path, Skill Skill);
