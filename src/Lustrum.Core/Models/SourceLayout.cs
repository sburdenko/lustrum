namespace Lustrum.Core.Models;

/// <summary>
/// Standard sources keep skills in skills/&lt;name&gt;/SKILL.md and commands in commands/. Skills synced from
/// a claude.ai account sit directly in the account folder: &lt;account&gt;/&lt;name&gt;/SKILL.md.
/// </summary>
public enum SourceLayout { Standard, Flat }
