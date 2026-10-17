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

    partial void OnCardChanged(SkillCardViewModel? value) => OnPropertyChanged(nameof(HasCard));

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
            .FirstOrDefault(c => c.Name == item.Skill.Name)?.Skills
            .Where(s => s.Id != item.Skill.Id)
            .ToList() ?? [];

        var card = new SkillCardViewModel(item.Skill, others, _effective?.Find(item.Skill.Id), _effective?.Project.DisplayName);
        card.ApplyUsage(UsageState);
        card.SelectedTab = _cardTab == SkillCardViewModel.CompareTab && !card.HasConflicts ? 0 : _cardTab;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SkillCardViewModel.SelectedTab))
            {
                _cardTab = card.SelectedTab;
            }
        };
        Card = card;
        _ = LoadBodyAsync(card);
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
