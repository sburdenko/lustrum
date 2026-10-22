using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.Core.Models;
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
        var entry = _effective?.Find(item.Skill.Id);
        var card = new SkillCardViewModel(item.Skill, others, entry, project?.DisplayName, _missing?.Find(item.Skill.Id))
        {
            Number = _numbers.GetValueOrDefault(item.Skill.Id),
            Issue = IssueFor(item.Skill),
            Context = ContextPanel.For(item.Skill, entry, entry?.VisibilityValue ?? HomeVisibility(item.Skill)),
            Checks = CheckRow.For(item.Skill),
        };
        card.ApplyUsage(UsageState);
        card.SelectedTab = _cardTab == SkillCardViewModel.CompareTab && !card.HasConflicts ? 0 : _cardTab;
        card.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(Card, card) && e.PropertyName == nameof(SkillCardViewModel.SelectedTab) && card.SelectedTab >= 0)
            {
                _cardTab = card.SelectedTab;
                OnPropertyChanged(nameof(CliCommand));
            }
        };
        Card = card;
        PrepareWrites(card);
        _ = LoadBodyAsync(card);
        StartGitLoad(card);
    }

    private string? IssueFor(Skill skill)
    {
        if (skill.Health == Health.DescriptionTooLong)
        {
            return $"Description is {skill.Description?.Length ?? 0:N0} characters; the limit is 1,024.";
        }

        if (!skill.IsBroken)
        {
            return null;
        }

        var detail = _result.Issues.FirstOrDefault(i => i.Path == skill.Path)?.Message;
        var label = HealthPresentation.For(skill.Health).Text;
        return detail is null ? label : $"{label}: {detail}";
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
