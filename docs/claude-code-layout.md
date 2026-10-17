# Claude Code on disk

What Lustrum relies on, as observed on real Claude Code installs in October 2026.
Most of these formats are not documented by Anthropic and can change between releases.
The code that depends on each point is named, so a change has one place to land.

## Skills and commands

```text
~/.claude/skills/<folder>/SKILL.md        one level deep; Claude Code does not look deeper
~/.claude/commands/<name>.md              legacy slash command, no frontmatter required
~/.claude/commands/<group>/<name>.md      namespaced command, invoked as /<group>:<name>
<project>/.claude/skills|commands         same layout, scoped to one project
```

`SKILL.md` starts with YAML frontmatter between `---` fences. `name` and `description` are the fields Lustrum
treats as required for skills; `allowed-tools` may be a YAML list or a comma-separated string.

Code: `SkillReader`, `FrontmatterParser`.

## Plugins

Installed plugins live in a cache, several versions side by side:

```text
~/.claude/plugins/cache/<marketplace>/<plugin>/<version>/
    .claude-plugin/plugin.json
    skills/<name>/SKILL.md
    commands/<name>.md
    .orphaned_at                      present on versions that are no longer installed
```

`~/.claude/plugins/installed_plugins.json` is the list of what is actually installed:

```json
{
  "version": 2,
  "plugins": {
    "<plugin>@<marketplace>": [
      { "scope": "user", "installPath": "/abs/path/to/cache/<marketplace>/<plugin>/<version>", "version": "..." }
    ]
  }
}
```

`~/.claude/plugins/marketplaces/` is the catalogue of what could be installed, not what is.

`~/.claude/settings.json` can switch plugins off:

```json
{ "enabledPlugins": { "<plugin>@<marketplace>": false } }
```

Only an explicit `false` counts as disabled; a plugin that is not listed is treated as enabled.

Code: `PluginDiscovery` (registry, cache fallback, `enabledPlugins`).

## Synced from claude.ai

The Claude desktop app copies the skills and plugins of the signed-in claude.ai account to disk:

```text
~/.claude/skills/synced/<account>/<name>/SKILL.md     invoked as anthropic-skills:<name>
~/.claude/skills/synced/<account>/manifest.json
~/.claude/skills/synced/<account>/.staging/           in-progress sync, ignored
~/.claude/plugins/synced/<account>/<plugin>/          same layout as an installed plugin
~/.claude/plugins/synced/<account>/<plugin>~g2/       a newer generation of the same plugin
```

A plugin folder may be named after the plugin or after an id; the name comes from `.claude-plugin/plugin.json`.
When several generations exist, Lustrum reads the highest `~gN` (no suffix counts as 1).

Code: `SyncedDiscovery`.

## Projects

Two places know about projects:

- `~/.claude.json`, field `projects`: an object keyed by absolute project path.
- `~/.claude/projects/<encoded>/`: one folder per project Claude Code has run in.

`<encoded>` is the absolute path with every character outside `[A-Za-z0-9]` replaced by `-`:

| Path | Folder name |
|---|---|
| `/Users/me/dev/my_app` | `-Users-me-dev-my-app` |
| `/Users/me/dev/site.io` | `-Users-me-dev-site-io` |
| `/Users/me/dev/app/.claude/worktrees/x` | `-Users-me-dev-app--claude-worktrees-x` |
| `C:\work\app` | `C--work-app` |

The mapping is not reversible: `my-app`, `my_app` and `my/app` all encode the same way. Lustrum therefore
prefers exact paths from `~/.claude.json` and otherwise walks the real directory tree, keeping only branches
whose re-encoded name matches.

The home directory also appears as a project. Its `.claude` folder is the global source, so it is skipped
to avoid listing every global skill twice.

Code: `ProjectPathDecoder`, `ProjectDiscovery`.
