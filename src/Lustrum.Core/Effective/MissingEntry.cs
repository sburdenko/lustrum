using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record MissingEntry(Skill Skill, Skill? LoadedHere)
{
    /// <summary>
    /// Why the entry deserves a note beyond "not loaded here", or null when its source already says it all.
    /// </summary>
    public string? Reason(Func<SkillSource, string> sourceName)
    {
        if (Skill.Source.IsNamespaced && !Skill.Source.IsEnabled)
        {
            return "plugin disabled";
        }

        return LoadedHere is { } here ? $"loaded here from {sourceName(here.Source)}" : null;
    }
}
