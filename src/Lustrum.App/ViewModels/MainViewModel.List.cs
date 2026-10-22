using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Budget;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly IReadOnlyList<SortOption> SortChoices =
    [
        new(SortMode.Relevance, "Best match"),
        new(SortMode.Name, "By name"),
        new(SortMode.Source, "By source"),
        new(SortMode.Modified, "By date"),
        new(SortMode.LastUsed, "By last use"),
        new(SortMode.ContextCost, "By context cost"),
    ];

    private bool _isRefreshingList;
    private EffectiveSet? _effective;
    private MissingSet? _missing;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private SortOption _selectedSort = SortChoices[0];

    [ObservableProperty]
    private IReadOnlyList<SkillItemViewModel> _visibleSkills = [];

    [ObservableProperty]
    private IReadOnlyList<ChipViewModel> _chips = [];

    [ObservableProperty]
    private IReadOnlyList<IListRow> _listRows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBudget))]
    private BudgetBanner? _budget;

    public bool HasBudget => Budget is not null;

    private Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private Dictionary<string, int> _copies = new(StringComparer.Ordinal);
    private Dictionary<string, ProjectContext> _contexts = new(StringComparer.Ordinal);

    public IReadOnlyList<SortOption> Sorts => SortChoices;

    public bool IsEmptyResult => !IsScanning && !IsProblemsView && VisibleSkills.Count == 0;

    public string ListTitle => (ActiveView, SelectedProject) switch
    {
        (ProjectView.Effective, { } project) => $"{project.DisplayName} · effective set",
        (ProjectView.Missing, { } project) => $"{project.DisplayName} · missing",
        _ => SelectedNode?.Title ?? "All",
    };

    private bool MatchesChips(Skill skill) => Chips.Where(c => c.IsActive).All(c => c.Filter.Matches(skill, ChipContext));

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        RefreshList();
        StartBodySearch();
    }

    partial void OnSelectedSortChanged(SortOption value) => RefreshList();

    partial void OnVisibleSkillsChanged(IReadOnlyList<SkillItemViewModel> value) =>
        OnPropertyChanged(nameof(IsEmptyResult));

    private IReadOnlyList<ChipViewModel> BuildChips(IEnumerable<string> activeLabels)
    {
        var chips = FilterChips.Build(_settings.OwnPrefixes, activeLabels, ChipContext);
        foreach (var chip in chips)
        {
            chip.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ChipViewModel.IsOn))
                {
                    RefreshList();
                }
            };
        }

        return chips;
    }

    private void RefreshList()
    {
        var keepId = SelectedSkill?.Skill.Id;
        var project = SelectedProject;
        var context = project is not null && ActiveView != ProjectView.Own ? ContextFor(project) : null;
        _effective = ActiveView == ProjectView.Effective && context is not null ? EffectiveSetBuilder.Build(_result, context) : null;
        _missing = ActiveView == ProjectView.Missing && context is not null ? MissingSetBuilder.Build(_result, context) : null;
        Budget = _effective is null ? null : new BudgetBanner(ContextBudget.Summarize(_effective));
        var scope = ActiveScope(context);
        var found = SkillIndex.Search(_result.Skills, new SkillQuery(scope, SearchText)).Where(MatchesChips).ToList();
        var withBody = WithBodyMatches(found, scope);

        _isRefreshingList = true;
        try
        {
            var grouped = IsGrouped(scope);
            var ordered = grouped ? Sort(withBody).OrderBy(s => s.Source.Kind).ToList() : Sort(withBody).ToList();
            VisibleSkills = ordered.Select(Row).ToList();
            ListRows = BuildRows(VisibleSkills, grouped);
            SelectedSkill = VisibleSkills.FirstOrDefault(s => s.Skill.Id == keepId);
        }
        finally
        {
            _isRefreshingList = false;
        }

        RefreshProblems();
        ShowCard(SelectedSkill);
    }

    public void MoveSelection(int delta)
    {
        if (IsProblemsView)
        {
            SelectedProblem = Step(VisibleProblems, SelectedProblem is null ? -1 : IndexOf(VisibleProblems, p => ReferenceEquals(p, SelectedProblem)), delta);
            return;
        }

        var current = SelectedSkill?.Skill.Id;
        SelectedSkill = Step(VisibleSkills, current is null ? -1 : IndexOf(VisibleSkills, s => s.Skill.Id == current), delta);
    }

    private static T? Step<T>(IReadOnlyList<T> items, int index, int delta)
        where T : class
    {
        if (items.Count == 0)
        {
            return null;
        }

        var next = index < 0 ? (delta > 0 ? 0 : items.Count - 1) : Math.Clamp(index + delta, 0, items.Count - 1);
        return items[next];
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> match)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (match(items[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public bool SelectSection(string key)
    {
        if (NavSections.FirstOrDefault(s => s.Key == key) is not { } section)
        {
            return false;
        }

        SelectedNode = section.Node;
        return true;
    }

    private SkillItemViewModel Row(Skill skill) =>
        new(skill, _effective?.Find(skill.Id), _missing?.Find(skill.Id), _updates.Behind(skill), HomeVisibility(skill))
        {
            Number = _numbers.GetValueOrDefault(skill.Id),
            Copies = _copies.GetValueOrDefault(skill.ConflictKey, 1),
            ListedTokens = _effective?.Find(skill.Id) is { } entry ? ContextBudget.Tokens(ContextBudget.ListedChars(entry)) : null,
        };

    private int ListedChars(Skill skill) =>
        _effective?.Find(skill.Id) is { } entry ? ContextBudget.ListedChars(entry) : ContextBudget.ListedChars(skill, HomeVisibility(skill));

    private bool IsGrouped(SkillScope scope) =>
        SearchText.Trim().Length == 0
        && SelectedSort.Mode is SortMode.Relevance or SortMode.Source
        && scope is AllScope or EffectiveScope or MissingScope;

    private IReadOnlyList<IListRow> BuildRows(IReadOnlyList<SkillItemViewModel> items, bool grouped)
    {
        if (!grouped)
        {
            return items.Count == 0 ? [] : [new ListHeader(ListTitle.ToUpperInvariant(), items.Count), .. items];
        }

        return items
            .GroupBy(i => i.Skill.Source.Kind)
            .SelectMany(g => new IListRow[] { new ListHeader(GroupTitle(g.Key), g.Count()) }.Concat(g))
            .ToList();
    }

    private static string GroupTitle(SourceKind kind) => kind switch
    {
        SourceKind.Managed => "MANAGED",
        SourceKind.Global => "GLOBAL",
        SourceKind.Project => "PROJECTS",
        SourceKind.Plugin => "PLUGINS",
        SourceKind.Synced => "CLAUDE.AI",
        _ => kind.ToString().ToUpperInvariant(),
    };

    // Built once per scan and project: it probes the file system, and the list refreshes on every keystroke.
    private ProjectContext ContextFor(SkillSource project)
    {
        if (!_contexts.TryGetValue(project.Id, out var context))
        {
            context = ProjectContext.For(_result, project);
            _contexts[project.Id] = context;
        }

        return context;
    }

    private void IndexResult(ScanResult result)
    {
        _contexts = new Dictionary<string, ProjectContext>(StringComparer.Ordinal);
        _numbers = result.Skills.Select((s, i) => (s.Id, i)).ToDictionary(p => p.Id, p => p.i + 1, StringComparer.Ordinal);
        _copies = result.Conflicts.ToDictionary(c => c.Name, c => c.Skills.Count, StringComparer.Ordinal);
    }

    private SkillVisibility HomeVisibility(Skill skill) =>
        SettingsLayers.Applies(skill) ? _result.Settings.ResolveAtHome(skill).Value : SkillVisibility.On;

    private IEnumerable<Skill> Sort(IEnumerable<Skill> skills) => SelectedSort.Mode switch
    {
        SortMode.Name => skills.OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.Source => skills
            .OrderBy(s => s.Source.Kind)
            .ThenBy(s => s.Source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.Modified => skills.OrderByDescending(s => s.Modified),
        SortMode.ContextCost => skills
            .OrderByDescending(ListedChars)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.LastUsed => skills
            .OrderByDescending(s => Usage?.For(s.FullName)?.LastUsed ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        _ => skills,
    };
}
