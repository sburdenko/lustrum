using System.Text;
using System.Text.Json;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Tests.Fixtures;

public sealed class FakeClaudeHome : IDisposable
{
    private readonly string _root = Path.Combine(LongTempPath(), "lustrum-tests-" + Guid.NewGuid().ToString("N"));

    public FakeClaudeHome()
    {
        Home = Path.Combine(_root, "home");
        Directory.CreateDirectory(ClaudeDir);
    }

    public string Home { get; }

    public string ClaudeDir => Path.Combine(Home, ".claude");

    public ClaudePaths Paths => new(Home);

    public string Dir(string relativeToRoot)
    {
        var path = Path.Combine(_root, relativeToRoot.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    public string File(string absolutePath, string content, bool bom = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        System.IO.File.WriteAllText(absolutePath, content, new UTF8Encoding(bom));
        return absolutePath;
    }

    public string GlobalSkill(string folder, string content) =>
        File(Path.Combine(ClaudeDir, "skills", folder, "SKILL.md"), content);

    public string GlobalCommand(string name, string content) =>
        File(Path.Combine(ClaudeDir, "commands", name + ".md"), content);

    public string ProjectSkill(string projectDir, string folder, string content) =>
        File(Path.Combine(projectDir, ".claude", "skills", folder, "SKILL.md"), content);

    public string Project(string relativeToRoot) => Dir(relativeToRoot);

    public void RegisterProjectFolder(string projectDir) =>
        Directory.CreateDirectory(Path.Combine(ClaudeDir, "projects", ProjectPathDecoder.Encode(projectDir)));

    public void RegisterProjectFolderRaw(string encodedName) =>
        Directory.CreateDirectory(Path.Combine(ClaudeDir, "projects", encodedName));

    public void RegisterInClaudeJson(params string[] projectPaths)
    {
        var projects = projectPaths.ToDictionary(p => p, _ => new { });
        var json = JsonSerializer.Serialize(new { projects });
        File(Paths.ClaudeJson, json);
    }

    public string InstallPlugin(string marketplace, string plugin, string version, bool orphaned = false, bool register = true)
    {
        var installPath = Path.Combine(ClaudeDir, "plugins", "cache", marketplace, plugin, version);
        File(Path.Combine(installPath, ".claude-plugin", "plugin.json"), $$"""{ "name": "{{plugin}}", "version": "{{version}}" }""");
        if (orphaned)
        {
            File(Path.Combine(installPath, ".orphaned_at"), "1");
        }

        if (register)
        {
            RegisterPlugin(marketplace, plugin, installPath);
        }

        return installPath;
    }

    private void RegisterPlugin(string marketplace, string plugin, string installPath)
    {
        var existing = new Dictionary<string, object[]>();
        if (System.IO.File.Exists(Paths.InstalledPluginsJson))
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(Paths.InstalledPluginsJson));
            foreach (var entry in doc.RootElement.GetProperty("plugins").EnumerateObject())
            {
                existing[entry.Name] = entry.Value.EnumerateArray().Select(e => (object)e.Clone()).ToArray();
            }
        }

        existing[$"{plugin}@{marketplace}"] = [new { scope = "user", installPath, version = "x" }];
        File(Paths.InstalledPluginsJson, JsonSerializer.Serialize(new { version = 2, plugins = existing }));
    }

    public string SyncedSkill(string account, string folder, string content) =>
        File(Path.Combine(ClaudeDir, "skills", "synced", account, folder, "SKILL.md"), content);

    public string SyncedPlugin(string account, string folder, string name)
    {
        var root = Path.Combine(ClaudeDir, "plugins", "synced", account, folder);
        File(Path.Combine(root, ".claude-plugin", "plugin.json"), $$"""{ "name": "{{name}}" }""");
        return root;
    }

    public void EnablePlugins(params (string Key, bool Enabled)[] plugins)
    {
        var enabledPlugins = plugins.ToDictionary(p => p.Key, p => p.Enabled);
        File(Paths.SettingsJson, JsonSerializer.Serialize(new { enabledPlugins }));
    }

    /// <summary>
    /// Windows runners report the temp folder with 8.3 short names (RUNNER~1). The project path decoder
    /// walks real directory names, so the fixture root must use the long names.
    /// </summary>
    internal static string LongTempPath()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (!OperatingSystem.IsWindows())
        {
            return temp;
        }

        var current = new DirectoryInfo(Path.GetPathRoot(temp)!);
        foreach (var segment in temp[current.FullName.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.GetDirectories(segment).Single();
        }

        return current.FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
