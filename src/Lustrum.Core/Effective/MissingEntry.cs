using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

/// <summary>A skill the project does not load; <paramref name="PluginOff"/> when its plugin is off for this project.</summary>
public sealed record MissingEntry(Skill Skill, Skill? LoadedHere, bool PluginOff = false)
{
    /// <summary>
    /// Why the entry deserves a note beyond "not loaded here", or null when its source already says it all.
    /// </summary>
    public string? Reason(Func<SkillSource, string> sourceName)
    {
        var disabled = PluginOff ? "plugin disabled" : null;
        var loaded = LoadedHere is { } here ? $"loaded here from {sourceName(here.Source)}" : null;
        return (disabled, loaded) switch
        {
            ({ } d, { } l) => $"{d}; {l}",
            _ => disabled ?? loaded,
        };
    }
}
