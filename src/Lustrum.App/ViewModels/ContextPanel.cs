using Lustrum.Core.Budget;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

/// <summary>What the card's CONTEXT section says about the room a skill takes in Claude's context.</summary>
public sealed record ContextPanel(string Listed, string Invoked, string? Hint)
{
    private const int WorthHinting = 200;

    public bool HasHint => Hint is not null;

    public static ContextPanel For(Skill skill, EffectiveEntry? entry, SkillVisibility visibility)
    {
        var chars = ContextBudget.ListedChars(skill, visibility);
        var listed = entry?.Availability switch
        {
            Availability.Shadowed => "Not listed to Claude here: another copy takes the name",
            Availability.OnDemand => $"Listed once Claude works on files under {entry.LoadsUnder}: {Size(chars)}",
            _ when ContextBudget.KeptFromClaude(skill) => "Not listed to Claude: disable-model-invocation keeps it for /name only",
            _ when chars == 0 => "Not listed to Claude: its visibility hides it",
            _ => $"Listed to Claude in every session: {Size(chars)}",
        };
        var invoked = $"Invoking it adds SKILL.md: {skill.SizeBytes:N0} bytes, ≈{ContextBudget.Tokens(skill.SizeBytes):N0} tokens";
        var saving = SettingsLayers.Applies(skill) && entry?.Availability != Availability.Shadowed
            ? ContextBudget.NameOnlySaving(skill, visibility)
            : 0;
        var hint = saving >= WorthHinting ? $"Name only would save ≈{ContextBudget.Tokens(saving):N0} tokens per session" : null;
        return new ContextPanel(listed, invoked, hint);
    }

    private static string Size(int chars) => $"{chars:N0} characters, ≈{ContextBudget.Tokens(chars):N0} tokens";
}
