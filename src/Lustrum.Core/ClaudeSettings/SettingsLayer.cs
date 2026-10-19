using System.Collections.Immutable;

namespace Lustrum.Core.ClaudeSettings;

public sealed record SettingsLayer(SettingsScope Scope, string Path, ImmutableDictionary<string, SkillVisibility> Skills)
{
    public static SettingsLayer Empty(SettingsScope scope, string path) =>
        new(scope, path, ImmutableDictionary<string, SkillVisibility>.Empty.WithComparers(StringComparer.Ordinal));

    public SkillVisibility? Find(string skillName) => Skills.TryGetValue(skillName, out var value) ? value : null;
}
