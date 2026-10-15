using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Indexing;
using Lustrum.Core.Problems;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    private IReadOnlyList<ProblemItemViewModel> _visibleProblems = [];

    [ObservableProperty]
    private ProblemItemViewModel? _selectedProblem;

    public bool IsProblemsView => SelectedNode?.Scope is ProblemsScope;

    public bool HasNoProblems => IsProblemsView && VisibleProblems.Count == 0;

    partial void OnVisibleProblemsChanged(IReadOnlyList<ProblemItemViewModel> value) =>
        OnPropertyChanged(nameof(HasNoProblems));

    partial void OnSelectedProblemChanged(ProblemItemViewModel? value)
    {
        var skill = value?.Problem.Skill;
        SelectedSkill = skill is null
            ? null
            : VisibleSkills.FirstOrDefault(s => s.Skill.Id == skill.Id) ?? new SkillItemViewModel(skill);
    }

    private void RefreshProblems()
    {
        if (!IsProblemsView)
        {
            VisibleProblems = [];
            return;
        }

        var keep = SelectedProblem?.Problem;
        VisibleProblems = ProblemCollector.Collect(_result)
            .Select(p => new ProblemItemViewModel(p))
            .Where(p => p.Matches(SearchText.Trim()))
            .ToList();
        SelectedProblem = VisibleProblems.FirstOrDefault(p => p.Problem == keep);
    }
}
