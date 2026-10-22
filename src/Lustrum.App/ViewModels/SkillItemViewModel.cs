using FluentIcons.Common;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using CoreHealth = Lustrum.Core.Models.Health;

namespace Lustrum.App.ViewModels;

public sealed class SkillItemViewModel : IListRow
{
    public SkillItemViewModel(
        Skill skill,
        EffectiveEntry? entry = null,
        MissingEntry? missing = null,
        int behind = 0,
        SkillVisibility visibility = SkillVisibility.On)
    {
        Skill = skill;
        Behind = behind;
        Entry = entry;
        IsShadowed = entry?.Availability == Availability.Shadowed;
        IsDisabled = (entry, missing) switch
        {
            ({ } loaded, _) => loaded.IsTurnedOff,
            (_, { } absent) => absent.PluginOff,
            _ => IsPluginOff(skill) || visibility == SkillVisibility.Off,
        };
        AvailabilityLabel = missing is not null
            ? missing.Reason(SourceLabelFor)
            : LabelFor(skill, entry) ?? (entry is null ? visibility.Label() : null);
        Health = HealthPresentation.For(skill.Health);
        Prefix = skill.Kind == SkillKind.Command ? "/" : PluginPrefix(skill);
        Description = string.IsNullOrWhiteSpace(skill.Description) ? "no description" : skill.Description.ReplaceLineEndings(" ");
        HasDescription = !string.IsNullOrWhiteSpace(skill.Description);
        KindIcon = skill.Kind == SkillKind.Command ? Symbol.SlashForward : Symbol.Sparkle;
        SourceIcon = SourceSymbol(skill.Source.Kind);
        SourceLabel = SourceLabelFor(skill.Source);
    }

    public Skill Skill { get; }

    public bool IsSelectable => true;

    /// <summary>The skill's place in the whole scan, so a number keeps meaning the same skill across views.</summary>
    public int Number { get; init; }

    /// <summary>How many skills on the machine share this skill's name, itself included.</summary>
    public int Copies { get; init; } = 1;

    public string NumberText => Number.ToString("000");

    /// <summary>Estimated tokens the skill takes in Claude's skill list; set in the Effective view only.</summary>
    public int? ListedTokens { get; init; }

    public string CostText => ListedTokens is { } tokens ? $"≈{tokens}" : string.Empty;

    public bool HasCost => ListedTokens is not null;

    public string Mark => Skill.Health switch
    {
        CoreHealth.Ok when FirstCheck is { } check => check.Badge,
        CoreHealth.Ok => Copies > 1 ? $"×{Copies}" : string.Empty,
        CoreHealth.DescriptionTooLong => "LONG",
        CoreHealth.Orphaned => "GONE",
        _ => "ERR",
    };

    public bool HasMark => Mark.Length > 0;

    public bool IsMarkError => Health.IsError;

    public bool IsMarkWarning => Health.IsWarning || (Health.IsOk && FirstCheck is not null);

    public bool IsMarkQuiet => Health.IsOk && FirstCheck is null;

    public string MarkTip => Health.IsOk && FirstCheck is { } check ? check.Message : Health.Text;

    private CheckRow? FirstCheck => CheckRow.For(Skill).FirstOrDefault();

    public string Status => string.Join(" · ", new[] { AvailabilityLabel, HasUpdate ? $"↓{Behind}" : null }.OfType<string>());

    public bool HasStatus => Status.Length > 0;

    public EffectiveEntry? Entry { get; }

    public bool IsShadowed { get; }

    public bool IsDisabled { get; }

    public string? AvailabilityLabel { get; }

    public bool HasAvailabilityLabel => AvailabilityLabel is not null;

    public int Behind { get; }

    public bool HasUpdate => Behind > 0;

    public string UpdateHint => Behind == 1 ? "1 new commit upstream" : $"{Behind} new commits upstream";

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
        SourceKind.Managed => Symbol.ShieldCheckmark,
        SourceKind.Global => Symbol.Globe,
        SourceKind.Project => Symbol.Folder,
        SourceKind.Plugin => Symbol.PuzzlePiece,
        SourceKind.Synced => Symbol.Cloud,
        _ => Symbol.Box,
    };

    public static string SourceLabelFor(SkillSource source) => source.Kind switch
    {
        SourceKind.Managed => "Managed",
        SourceKind.Global => "Global",
        SourceKind.Plugin or SourceKind.Synced => source.Namespace ?? source.DisplayName,
        _ => source.DisplayName,
    };

    // Outside a project a plugin's own switch decides; inside one the effective set already knows.
    public static string? LabelFor(Skill skill, EffectiveEntry? entry) =>
        entry is not null ? entry.Label(SourceLabelFor) : IsPluginOff(skill) ? "disabled" : null;

    private static bool IsPluginOff(Skill skill) => skill.Source.Kind == SourceKind.Plugin && !skill.Source.IsEnabled;

    private static string PluginPrefix(Skill skill) =>
        skill.FullName.Length > skill.Name.Length ? skill.FullName[..(skill.FullName.Length - skill.Name.Length)] : string.Empty;
}
