using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Copying;

namespace Lustrum.App.Services;

/// <summary>The only writes Lustrum makes outside its own settings, each behind an explicit control.</summary>
public interface ISkillWriter
{
    /// <summary>Stores the entry for a skill name; null removes it.</summary>
    Task SetVisibilityAsync(string settingsPath, string skillName, SkillVisibility? visibility);

    Task<CopyResult> CopyAsync(ReadyCopy plan);
}
