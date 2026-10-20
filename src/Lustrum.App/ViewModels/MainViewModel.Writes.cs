using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Copying;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    private string _actionNotice = string.Empty;

    private void PrepareWrites(SkillCardViewModel card)
    {
        var project = _effective?.Project ?? _missing?.Project;
        card.ShowVisibility(VisibilityPanel.For(card.Skill, _result.Settings, _effective?.Find(card.Skill.Id), project));
        card.VisibilityRequested += (target, value) => _ = SetVisibilityAsync(target, value);
        card.CopyTargets = SkillCopier.Targets(_result, card.Skill).Select(t => CopyTargetFor(card.Skill, t)).ToList();
    }

    private CopyTarget CopyTargetFor(Skill skill, SkillSource target)
    {
        var plan = SkillCopier.Plan(_result, skill, target);
        var name = target.Kind == SourceKind.Global ? "Personal skills" : target.DisplayName;
        var label = plan is BlockedCopy blocked ? $"{name} ({blocked.Reason})" : name;
        return new CopyTarget(label, new AsyncRelayCommand(() => CopyAsync(skill, target), () => plan is ReadyCopy));
    }

    private async Task SetVisibilityAsync(SkillCardViewModel card, SkillVisibility value)
    {
        if (card.Visibility is not { } panel)
        {
            return;
        }

        card.IsSavingVisibility = true;
        try
        {
            var entry = _result.Settings.EntryFor(card.Skill, panel.Layer, value, panel.Session);
            await _services.Writer.SetVisibilityAsync(panel.Layer.Path, card.Skill.Name, entry);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not change {card.Skill.Name}: {ex.Message}";
            if (ReferenceEquals(Card, card))
            {
                card.ShowVisibility(panel);
            }

            return;
        }
        finally
        {
            card.IsSavingVisibility = false;
        }

        ActionNotice = $"{card.Skill.Name} is now {VisibilityOption.For(value).Label} in {Describe(panel.Layer)}";
        await ScanAsync();
    }

    private async Task CopyAsync(Skill skill, SkillSource target)
    {
        if (SkillCopier.Plan(_result, skill, target) is not ReadyCopy plan)
        {
            ErrorMessage = $"Could not copy {skill.Name}: the target changed, rescan and try again";
            return;
        }

        try
        {
            var result = await _services.Writer.CopyAsync(plan);
            var skipped = result.Skipped.Count == 0 ? string.Empty : $", left out {string.Join(", ", result.Skipped)}";
            ActionNotice = $"Copied {skill.Name} to {(target.Kind == SourceKind.Global ? "personal skills" : target.DisplayName)}{skipped}";
            await ScanAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not copy {skill.Name}: {ex.Message}";
        }
    }

    private static string Describe(SettingsLayer layer) => layer.Scope switch
    {
        SettingsScope.User => "your user settings",
        SettingsScope.Managed => "managed settings",
        _ => Path.GetFileName(layer.Path),
    };
}
