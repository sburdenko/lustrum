namespace Lustrum.Core.Scanning;

public sealed record ClaudePaths(string Home)
{
    public string ClaudeDir => Path.Combine(Home, ".claude");

    public string SkillsDir => Path.Combine(ClaudeDir, "skills");

    public string CommandsDir => Path.Combine(ClaudeDir, "commands");

    public string ProjectsDir => Path.Combine(ClaudeDir, "projects");

    public string PluginsDir => Path.Combine(ClaudeDir, "plugins");

    public string PluginCacheDir => Path.Combine(PluginsDir, "cache");

    public string InstalledPluginsJson => Path.Combine(PluginsDir, "installed_plugins.json");

    public string SettingsJson => Path.Combine(ClaudeDir, "settings.json");

    public string ClaudeJson => Path.Combine(Home, ".claude.json");

    public static ClaudePaths ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
