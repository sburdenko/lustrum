using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.Core.Scanning;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand), nameof(CopyPathCommand), nameof(CopyInvocationCommand), nameof(OpenInEditorCommand))]
    private SkillItemViewModel? _selectedSkill;

    [ObservableProperty]
    private SkillCardViewModel? _card;

    private int _cardTab;

    public bool HasCard => Card is not null;

    partial void OnCardChanged(SkillCardViewModel? value)
    {
        OnPropertyChanged(nameof(HasCard));
        RefreshGitCommands();
    }

    partial void OnSelectedSkillChanged(SkillItemViewModel? value)
    {
        if (!_isRefreshingList)
        {
            ShowCard(value);
        }
    }

    private void ShowCard(SkillItemViewModel? item)
    {
        if (item is null)
        {
            Card = null;
            return;
        }

        if (Card?.Skill.Id == item.Skill.Id)
        {
            return;
        }

        var others = _result.Conflicts
            .FirstOrDefault(c => c.Name == item.Skill.ConflictKey)?.Skills
            .Where(s => s.Id != item.Skill.Id)
            .ToList() ?? [];

        var project = _effective?.Project ?? _missing?.Project;
        var card = new SkillCardViewModel(item.Skill, others, _effective?.Find(item.Skill.Id), project?.DisplayName, _missing?.Find(item.Skill.Id));
        card.ApplyUsage(UsageState);
        card.SelectedTab = _cardTab == SkillCardViewModel.CompareTab && !card.HasConflicts ? 0 : _cardTab;
        card.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(Card, card) && e.PropertyName == nameof(SkillCardViewModel.SelectedTab) && card.SelectedTab >= 0)
            {
                _cardTab = card.SelectedTab;
            }
        };
        Card = card;
        PrepareWrites(card);
        _ = LoadBodyAsync(card);
        StartGitLoad(card);
    }

    private static async Task LoadBodyAsync(SkillCardViewModel card)
    {
        try
        {
            card.Body = await Task.Run(() => SkillBodyReader.Read(card.Skill));
        }
        catch (Exception ex)
        {
            card.Body = $"Could not read the file: {ex.Message}";
        }
        finally
        {
            card.IsBodyLoading = false;
        }
    }
}
