namespace Lustrum.Core.Scanning;

/// <summary>
/// Where Claude Code keeps its files. <paramref name="ManagedRoot"/> overrides the system folder that holds managed
/// settings and managed skills, which tests point at a throw-away folder.
/// </summary>
public sealed record ClaudePaths(string Home, string? ManagedRoot = null)
{
    public string ManagedDir => ManagedRoot ?? DefaultManagedDir;

    public string ManagedSettingsJson => Path.Combine(ManagedDir, "managed-settings.json");

    public string ManagedSettingsDropIns => Path.Combine(ManagedDir, "managed-settings.d");

    public string ManagedClaudeDir => Path.Combine(ManagedDir, ".claude");

    public static string DefaultManagedDir =>
        OperatingSystem.IsMacOS() ? "/Library/Application Support/ClaudeCode"
        : OperatingSystem.IsWindows() ? @"C:\Program Files\ClaudeCode"
        : "/etc/claude-code";

    public string ClaudeDir => Path.Combine(Home, ".claude");

    public string SkillsDir => Path.Combine(ClaudeDir, "skills");

    public string CommandsDir => Path.Combine(ClaudeDir, "commands");

    public string SyncedSkillsDir => Path.Combine(SkillsDir, "synced");

    public string ProjectsDir => Path.Combine(ClaudeDir, "projects");

    public string PluginsDir => Path.Combine(ClaudeDir, "plugins");

    public string PluginCacheDir => Path.Combine(PluginsDir, "cache");

    public string SyncedPluginsDir => Path.Combine(PluginsDir, "synced");

    public string InstalledPluginsJson => Path.Combine(PluginsDir, "installed_plugins.json");

    public string SettingsJson => Path.Combine(ClaudeDir, "settings.json");

    /// <summary>Claude Code's cache of server-managed settings fetched from claude.ai or a gateway.</summary>
    public string RemoteSettingsJson => Path.Combine(ClaudeDir, "remote-settings.json");

    public string ClaudeJson => Path.Combine(Home, ".claude.json");

    public static ClaudePaths ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
