using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    private bool _showEffectiveSet;

    public bool CanShowEffectiveSet => SelectedProject is not null;

    private SkillSource? SelectedProject =>
        SelectedNode?.Scope is SourceScope scope
            ? _result.Sources.FirstOrDefault(s => s.Id == scope.SourceId && s.Kind == SourceKind.Project)
            : null;

    private SkillSource? EffectiveProject => ShowEffectiveSet ? SelectedProject : null;

    partial void OnShowEffectiveSetChanged(bool value)
    {
        Card = null;
        OnPropertyChanged(nameof(ListTitle));
        RefreshList();
    }
}
