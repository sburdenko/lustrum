using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Effective;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly IReadOnlyList<ProjectViewOption> ProjectViewChoices =
    [
        new(ProjectView.Own, "Own", "Skills stored in this project"),
        new(ProjectView.Effective, "Effective", "What Claude Code loads here: this project, global skills, enabled plugins and claude.ai skills"),
        new(ProjectView.Missing, "Missing", "Skills on this machine that this project does not load"),
    ];

    [ObservableProperty]
    private ProjectViewOption? _selectedProjectView = ProjectViewChoices[0];

    public IReadOnlyList<ProjectViewOption> ProjectViews => ProjectViewChoices;

    public bool CanChooseProjectView => SelectedProject is not null;

    private SkillSource? SelectedProject =>
        SelectedNode?.Scope is SourceScope scope
            ? _result.Sources.FirstOrDefault(s => s.Id == scope.SourceId && s.Kind == SourceKind.Project)
            : null;

    private ProjectView ActiveView => SelectedProject is null ? ProjectView.Own : SelectedProjectView?.View ?? ProjectView.Own;

    partial void OnSelectedProjectViewChanged(ProjectViewOption? value)
    {
        Card = null;
        OnPropertyChanged(nameof(ListTitle));
        RefreshList();
    }

    private SkillScope ActiveScope(ProjectContext? context) => (ActiveView, context) switch
    {
        (ProjectView.Effective, { } c) => new EffectiveScope(c),
        (ProjectView.Missing, { } c) => new MissingScope(c),
        _ => SelectedNode?.Scope ?? SkillScope.All,
    };
}
