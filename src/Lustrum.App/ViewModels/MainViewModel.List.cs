using CommunityToolkit.Mvvm.ComponentModel;
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

    public IReadOnlyList<SortOption> Sorts => SortChoices;

    public bool IsEmptyResult => !IsScanning && !IsProblemsView && VisibleSkills.Count == 0;

    public string ListTitle => (ActiveView, SelectedProject) switch
    {
        (ProjectView.Effective, { } project) => $"{project.DisplayName} · effective set",
        (ProjectView.Missing, { } project) => $"{project.DisplayName} · missing",
        _ => SelectedNode?.Title ?? "All",
    };

    private bool MatchesChips(Skill skill) => Chips.Where(c => c.IsActive).All(c => c.Filter.Matches(skill, Usage));

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
        var chips = FilterChips.Build(_settings.OwnPrefixes, activeLabels, UsageState.CanJudgeUnused);
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
        _effective = ActiveView == ProjectView.Effective && project is not null ? EffectiveSetBuilder.Build(_result, project) : null;
        _missing = ActiveView == ProjectView.Missing && project is not null ? MissingSetBuilder.Build(_result, project) : null;
        var scope = ActiveScope(project);
        var found = SkillIndex.Search(_result.Skills, new SkillQuery(scope, SearchText)).Where(MatchesChips).ToList();
        var withBody = WithBodyMatches(found, scope);

        _isRefreshingList = true;
        try
        {
            VisibleSkills = Sort(withBody).Select(s => new SkillItemViewModel(s, _effective?.Find(s.Id), _missing?.Find(s.Id))).ToList();
            SelectedSkill = VisibleSkills.FirstOrDefault(s => s.Skill.Id == keepId);
        }
        finally
        {
            _isRefreshingList = false;
        }

        RefreshProblems();
        ShowCard(SelectedSkill);
    }

    private IEnumerable<Skill> Sort(IEnumerable<Skill> skills) => SelectedSort.Mode switch
    {
        SortMode.Name => skills.OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.Source => skills
            .OrderBy(s => s.Source.Kind)
            .ThenBy(s => s.Source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.Modified => skills.OrderByDescending(s => s.Modified),
        SortMode.LastUsed => skills
            .OrderByDescending(s => Usage?.For(s.FullName)?.LastUsed ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        _ => skills,
    };
}
