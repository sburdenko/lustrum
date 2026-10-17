using FluentIcons.Common;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using CoreHealth = Lustrum.Core.Models.Health;

namespace Lustrum.App.ViewModels;

public sealed class SkillItemViewModel
{
    public SkillItemViewModel(Skill skill, EffectiveEntry? entry = null, MissingEntry? missing = null)
    {
        Skill = skill;
        Entry = entry;
        IsShadowed = entry?.Availability == Availability.Shadowed;
        IsDisabled = skill.Source.Kind == SourceKind.Plugin && !skill.Source.IsEnabled;
        AvailabilityLabel = missing is not null ? missing.Reason(SourceLabelFor) : LabelFor(skill, entry);
        Health = HealthPresentation.For(skill.Health);
        Prefix = skill.Kind == SkillKind.Command ? "/" : PluginPrefix(skill);
        Description = string.IsNullOrWhiteSpace(skill.Description) ? "no description" : skill.Description.ReplaceLineEndings(" ");
        HasDescription = !string.IsNullOrWhiteSpace(skill.Description);
        KindIcon = skill.Kind == SkillKind.Command ? Symbol.SlashForward : Symbol.Sparkle;
        SourceIcon = SourceSymbol(skill.Source.Kind);
        SourceLabel = SourceLabelFor(skill.Source);
    }

    public Skill Skill { get; }

    public EffectiveEntry? Entry { get; }

    public bool IsShadowed { get; }

    public bool IsDisabled { get; }

    public string? AvailabilityLabel { get; }

    public bool HasAvailabilityLabel => AvailabilityLabel is not null;

    public double RowOpacity => IsShadowed || IsDisabled ? 0.5 : 1.0;

    public HealthPresentation Health { get; }

    public string Prefix { get; }

    public string Name => Skill.Name;

    public string Description { get; }

    public bool HasDescription { get; }

    public Symbol KindIcon { get; }

    public Symbol SourceIcon { get; }

    public string SourceLabel { get; }

    public bool ShowHealth => Skill.Health != CoreHealth.Ok;

    public static Symbol SourceSymbol(SourceKind kind) => kind switch
    {
        SourceKind.Global => Symbol.Globe,
        SourceKind.Project => Symbol.Folder,
        SourceKind.Plugin => Symbol.PuzzlePiece,
        SourceKind.Synced => Symbol.Cloud,
        _ => Symbol.Box,
    };

    public static string SourceLabelFor(SkillSource source) => source.Kind switch
    {
        SourceKind.Global => "Global",
        SourceKind.Plugin or SourceKind.Synced => source.Namespace ?? source.DisplayName,
        _ => source.DisplayName,
    };

    public static string? LabelFor(Skill skill, EffectiveEntry? entry) =>
        entry?.Label(SourceLabelFor)
        ?? (skill.Source.Kind == SourceKind.Plugin && !skill.Source.IsEnabled ? "disabled" : null);

    private static string PluginPrefix(Skill skill) =>
        skill.FullName.Length > skill.Name.Length ? skill.FullName[..(skill.FullName.Length - skill.Name.Length)] : string.Empty;
}
