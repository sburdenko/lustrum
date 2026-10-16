using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lustrum.Screenshots;

/// <summary>
/// Builds a synthetic home folder with a ~/.claude tree, projects, plugins and transcripts, so screenshots
/// never show a real machine.
/// </summary>
internal sealed partial class DemoHome : IDisposable
{
    private const string CodeReviewBody = """
        # Code review

        Review a change set the way a careful teammate would: correctness first, then clarity.

        ## When to use

        - A pull request or a local diff needs a second pair of eyes
        - You want findings ranked by severity, not a wall of nits

        ## Steps

        1. Read the diff end to end before commenting
        2. Group findings by **severity**: blocker, should-fix, nit
        3. For each finding give the file, the line and a concrete fix

        ## Output

        | Severity | Meaning |
        |---|---|
        | Blocker | Wrong behaviour or data loss |
        | Should-fix | Likely to bite later |
        | Nit | Optional polish |

        > Prefer one precise finding over five vague ones.

        ```bash
        git diff main...HEAD --stat
        ```
        """;

    private static readonly JsonSerializerOptions LikeClaudeCode = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Dictionary<string, object> _plugins = [];

    public DemoHome()
    {
        Home = Path.Combine(Path.GetTempPath(), "lustrum-demo-" + Guid.NewGuid().ToString("N"));
        Build();
    }

    public string Home { get; }

    private string Claude => Path.Combine(Home, ".claude");

    private string Work(string name) => Path.Combine(Home, "work", name);

    public void Dispose() => Directory.Delete(Home, recursive: true);

    private void Build()
    {
        GlobalSkills();
        Projects();
        Plugins();
        Transcripts();
    }

    private void GlobalSkills()
    {
        Skill(Claude, "code-review", "Review a diff for correctness, clarity and risk, with findings ranked by severity.",
            "allowed-tools: Read, Grep, Bash(git:*)\n", CodeReviewBody, "checklists/security.md", "checklists/performance.md", "scripts/diff-stats.sh");
        Skill(Claude, "release-notes", "Draft release notes from merged pull requests since the last tag.",
            "allowed-tools: Bash(git:*), Bash(gh:*)\n", null, "templates/notes.md");
        Skill(Claude, "sql-helper", "Write and explain SQL, check query plans and suggest indexes.");
        Skill(Claude, "sb-daily-notes", "Capture the day as short structured notes and carry open items forward.");
        Skill(Claude, "sb-meeting-prep", "Prepare an agenda and talking points from the attendee list and previous notes.");
        Skill(Claude, "pdf-forms", "Fill and flatten PDF forms from structured data.", "license: MIT\nversion: 1.2.0\n");
        Skill(Claude, "brand-voice", "Rewrite copy in the house tone of voice. " + string.Concat(Enumerable.Repeat("Applies the style guide, banned phrases and examples. ", 30)));
        Write(Path.Combine(Claude, "skills", "legacy-import", "SKILL.md"), "Imports old markdown notes.\nNo frontmatter here.\n");
        Write(Path.Combine(Claude, "skills", "broken-yaml", "SKILL.md"), "---\nname: [oops\ndescription: unclosed flow sequence\n---\nbody\n");
        Write(Path.Combine(Claude, "commands", "deploy.md"), "---\ndescription: Run the deploy checklist and ship to staging\n---\nRun the checklist, then deploy.\n");
        Write(Path.Combine(Claude, "commands", "git", "squash.md"), "Squash the current branch into one clean commit.\n");
    }

    private void Projects()
    {
        var known = new List<string>();
        Project(known, "storefront",
            ("checkout-flow", "Trace the checkout flow and find where an order can get stuck."),
            ("ui-kit", "Use the shared component library correctly: tokens, spacing, accessibility."),
            ("release-notes", "Release notes for the storefront: customer-facing wording, no ticket ids."));
        Project(known, "mobile-app",
            ("crash-triage", "Group crash reports by root cause and propose the smallest fix."),
            ("store-listing", "Keep store listing text and screenshots consistent across locales."),
            ("release-notes", "Release notes for the mobile app, trimmed to the store character limit."));
        Project(known, "docs-site", ("writing-style", "Apply the docs style guide to new pages."));
        Project(known, "scratch");
        known.Add(Work("retired-prototype"));

        foreach (var path in known)
        {
            Directory.CreateDirectory(Path.Combine(Claude, "projects", Encode(path)));
        }

        Write(Path.Combine(Home, ".claude.json"), JsonSerializer.Serialize(new { projects = known.ToDictionary(p => p, _ => new { }) }));
    }

    private void Plugins()
    {
        Plugin("acme-tools", "review-kit", "2.1.0",
            ("security-scan", "Scan changed files for injection, secrets and unsafe deserialization."),
            ("perf-audit", "Find hot paths and allocation-heavy code in a profile or diff."));
        Plugin("acme-tools", "data-kit", "0.9.3",
            ("csv-clean", "Normalise encodings, delimiters and column names in messy CSV files."),
            ("chart-builder", "Turn a table into an accessible chart with sensible defaults."));
        Write(Path.Combine(Claude, "plugins", "cache", "acme-tools", "data-kit", "0.9.3", "commands", "export.md"), "Export the current dataset to CSV or Parquet.\n");
        Plugin("field-guides", "ops-runbooks", "1.0.0",
            ("incident-response", "Walk through an incident: triage, comms, mitigation, follow-up."));
        Write(Path.Combine(Claude, "plugins", "installed_plugins.json"), JsonSerializer.Serialize(new { version = 2, plugins = _plugins }));
    }

    private void Transcripts()
    {
        var store = Work("storefront");
        var mobile = Work("mobile-app");
        Transcript(store,
            Model("release-notes", store, "2026-10-02T09:12:00Z"),
            Model("release-notes", store, "2026-10-09T15:40:00Z"),
            User("release-notes", store, "2026-10-12T11:05:00Z"),
            Model("code-review", store, "2026-10-10T10:00:00Z"),
            User("deploy", store, "2026-10-11T17:30:00Z"),
            Model("data-kit:csv-clean", store, "2026-09-20T08:00:00Z"));
        Transcript(mobile,
            Model("release-notes", mobile, "2026-10-05T13:00:00Z"),
            Model("code-review", mobile, "2026-10-14T09:00:00Z"),
            Model("crash-triage", mobile, "2026-10-13T16:20:00Z"));
    }

    private void Project(List<string> known, string name, params (string Name, string Description)[] skills)
    {
        var path = Work(name);
        Directory.CreateDirectory(path);
        foreach (var (skill, description) in skills)
        {
            Skill(Path.Combine(path, ".claude"), skill, description);
        }

        known.Add(path);
    }

    private void Plugin(string marketplace, string name, string version, params (string Name, string Description)[] skills)
    {
        var installPath = Path.Combine(Claude, "plugins", "cache", marketplace, name, version);
        Write(Path.Combine(installPath, ".claude-plugin", "plugin.json"), JsonSerializer.Serialize(new { name, version }));
        foreach (var (skill, description) in skills)
        {
            Skill(installPath, skill, description);
        }

        _plugins[$"{name}@{marketplace}"] = new[] { new { scope = "user", installPath, version } };
    }

    private void Transcript(string project, params string[] lines) =>
        Write(Path.Combine(Claude, "projects", Encode(project), "demo-session.jsonl"), string.Join('\n', lines) + "\n");

    private static string Model(string skill, string cwd, string at) => JsonSerializer.Serialize(new
    {
        type = "assistant",
        timestamp = at,
        cwd,
        message = new { role = "assistant", content = new object[] { new { type = "tool_use", id = "t", name = "Skill", input = new { skill } } } },
    }, LikeClaudeCode);

    private static string User(string command, string cwd, string at) => JsonSerializer.Serialize(new
    {
        type = "user",
        timestamp = at,
        cwd,
        message = new { role = "user", content = $"<command-name>/{command}</command-name>" },
    }, LikeClaudeCode);

    private static void Skill(string root, string name, string description, string extra = "", string? body = null, params string[] files)
    {
        var folder = Path.Combine(root, "skills", name);
        Write(Path.Combine(folder, "SKILL.md"), $"---\nname: {name}\ndescription: {JsonSerializer.Serialize(description)}\n{extra}---\n{body ?? $"# {name}\n\n{description}\n"}");
        foreach (var file in files)
        {
            Write(Path.Combine(folder, file), "x\n");
        }
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Encode(string path) => NonAlphanumeric().Replace(path, "-");

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();
}
