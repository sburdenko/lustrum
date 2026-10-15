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
    ];

    private bool _isRefreshingList;
    private EffectiveSet? _effective;

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

    public string ListTitle => EffectiveProject is { } project
        ? $"{project.DisplayName} · effective set"
        : SelectedNode?.Title ?? "All";

    private IReadOnlyList<string> RequiredTags => Chips.Where(c => c.IsOn).Select(c => c.Tag).ToList();

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

    private IReadOnlyList<ChipViewModel> BuildChips(IEnumerable<string> activeTags)
    {
        var chips = FilterChips.Build(_settings.OwnPrefixes, activeTags);
        foreach (var chip in chips)
        {
            chip.PropertyChanged += (_, _) => RefreshList();
        }

        return chips;
    }

    private void RefreshList()
    {
        var keepId = SelectedSkill?.Skill.Id;
        var project = EffectiveProject;
        _effective = project is null ? null : EffectiveSetBuilder.Build(_result, project);
        var scope = project is null ? SelectedNode?.Scope ?? SkillScope.All : new EffectiveScope(project.Id);
        var tags = RequiredTags;
        var found = SkillIndex.Search(_result.Skills, new SkillQuery(scope, SearchText, tags));
        var withBody = WithBodyMatches(found, scope, tags);

        _isRefreshingList = true;
        try
        {
            VisibleSkills = Sort(withBody).Select(s => new SkillItemViewModel(s, _effective?.Find(s.Id))).ToList();
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
        _ => skills,
    };
}
