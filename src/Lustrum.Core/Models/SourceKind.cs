namespace Lustrum.Core.Models;

/// <summary>Where a skill comes from, in Claude Code's order of precedence for plain names: managed first.</summary>
public enum SourceKind { Managed, Global, Project, Plugin, Synced, BuiltIn }
