namespace Lustrum.Core.Linting;

public enum FindingKind
{
    /// <summary>The skill links to a file in its own folder that does not exist.</summary>
    MissingFile,

    /// <summary>An allowed-tools entry is malformed: empty, unbalanced parentheses, an empty pattern.</summary>
    ToolSyntax,

    /// <summary>An allowed-tools entry names a tool Claude Code does not have, as far as Lustrum knows.</summary>
    UnknownTool,

    /// <summary>Bash is allowed without a command pattern, so every command runs without asking.</summary>
    BroadTool,
}
