# Lustrum architecture

How Lustrum finds skills, models them, derives every view and writes back to disk. The [README](../README.md) has
the overview; this page has the details. For the Claude Code files themselves, see
[claude-code-layout.md](claude-code-layout.md).

- [Projects and dependencies](#projects-and-dependencies)
- [Discovery](#discovery)
- [Scan pipeline](#scan-pipeline)
- [Data model](#data-model)
- [Health](#health)
- [Effective set and Missing](#effective-set-and-missing)
- [Search and scopes](#search-and-scopes)
- [Usage](#usage)
- [Compare](#compare)
- [Git](#git)
- [Visibility](#visibility)
- [Copy](#copy)
- [Live updates](#live-updates)
- [Threading and failures](#threading-and-failures)
- [The window](#the-window)
- [Testing](#testing)

## Projects and dependencies

| Project | Depends on | Role |
|---|---|---|
| `Lustrum.Core` | YamlDotNet, DiffPlex | discovery, scanning, parsing, indexing, effective sets, linting, context budget, problems, usage, comparison, git, Claude Code settings, copying, watching |
| `Lustrum.App` | Core, Avalonia 11, CommunityToolkit.Mvvm, Markdown.Avalonia, Serilog | the window |
| `Lustrum.Cli` | Core, System.CommandLine | `list` and `diff` |
| `tools/Lustrum.Screenshots` | App, Avalonia.Headless | renders the README screenshots from a synthetic home |

Core has no UI dependency. Front ends never touch the file system for skills themselves; they ask Core.

## Discovery

| Source | Location | Skill name |
|---|---|---|
| Managed skills | `.claude/skills/<folder>/SKILL.md` in the managed settings folder: `/Library/Application Support/ClaudeCode` (macOS), `/etc/claude-code` (Linux), `C:\Program Files\ClaudeCode` (Windows) | as for global skills |
| Global skills | `~/.claude/skills/<folder>/SKILL.md` | `name` from frontmatter, else the folder name |
| Global commands | `~/.claude/commands/**/*.md` (up to 3 levels) | file path without `.md`, folders joined with `:` (`git/squash.md` is `/git:squash`) |
| Project skills and commands | `<project>/.claude/skills`, `<project>/.claude/commands` | same rules as global |
| Parent folders | every folder above a project, up to its repository root, that has a `.claude/skills` folder | same rules as global |
| Nested folders | folders below a project, to the search depth, that have a `.claude/skills` folder | same rules as global |
| Plugins | the `installPath` of every entry in `~/.claude/plugins/installed_plugins.json` | `<plugin>:<name>` |
| claude.ai skills | `~/.claude/skills/synced/<account>/<folder>/SKILL.md` | `anthropic-skills:<name>` |
| claude.ai plugins | `~/.claude/plugins/synced/<account>/<plugin>/` (newest `~gN` generation) | `<plugin>:<name>` |

**Projects.** `SourceDiscovery` merges three lists by normalised path:

1. the keys of `projects` in `~/.claude.json`;
2. the folder names in `~/.claude/projects`;
3. folders with a `.claude` directory under the extra roots from Settings (searched to the configured depth,
   default 4, skipping `node_modules`, `Library`, `bin`, `obj`, `.git`).

The home directory is skipped, because its `.claude` is already the global source. A project whose folder no longer
exists stays in the data with `Exists = false`; the source column hides it until **show empty and missing projects**
is ticked, and the Problems list shows it.

**Worktrees.** A linked git worktree (its `.git` is a file whose `gitdir` leads to `.git/worktrees/<name>`, with a
`commondir` back to the main repository) that has no `.claude/skills` folder of its own loads the main checkout's
project skills, as Claude Code does since 2.1.277. `GitWorktree.MainCheckout` finds that checkout without starting git;
submodules, whose `.git` file has no `commondir`, are not treated as worktrees.

**Parent and nested folders.** Claude Code loads project skills from the folder a session starts in and from every
parent up to the repository root, and skills in folders below it once Claude works on files there. For every known
project, `ProjectDiscovery.Related` adds the parent folders with skills of their own, stopping at the repository root
(the nearest `.git` entry; a project outside a repository gets no parents), and the folders below it to the search
depth, skipping dot folders and the folders excluded in Settings; a folder walked for one project is not walked again
for another. They become project sources of their own, deduplicated by normalised path, and a nested one is named
after its project, such as `storefront/apps/admin`. A folder below the project that sits in a different repository,
such as a worktree under `.claude/worktrees` or a nested clone, is a project of its own and never loads on demand.

**Managed.** A `.claude/skills` folder inside the managed settings folder becomes a source of kind `Managed`, shown
in its own section. Its skills take precedence over personal and project ones.

**Project folder names.** `~/.claude/projects` stores each path with every non-alphanumeric character replaced by
`-`. That mapping is lossy (`my-app`, `my_app` and `my/app` encode the same), so `ProjectPathDecoder` resolves a name
against the real file system: at each level it only follows directories whose own encoded name is a prefix of the
remaining string, preferring the longest match. Exact paths from `~/.claude.json` win over decoding. Names that
cannot be resolved are shown as missing, not guessed.

**Plugins.** The registry file is authoritative. Without it, Lustrum falls back to
`~/.claude/plugins/cache/<marketplace>/<plugin>/<version>` and ignores versions that carry an `.orphaned_at`
marker. Whether a plugin is on comes from `enabledPlugins`, keyed by `plugin@marketplace`, in managed, project-local,
project and user settings, the first file that mentions it winning. Outside a project only managed and user settings
count, and that state dims a disabled plugin in the lists; inside a project its own files can turn a plugin on or
off for that project alone.

**claude.ai skills and plugins.** The Claude desktop app syncs the skills and plugins of a claude.ai account,
including Anthropic's own skills such as `xlsx` or `skill-creator`, into `~/.claude/skills/synced` and
`~/.claude/plugins/synced`. Lustrum lists them as source kind `Synced`, under the names Claude uses to invoke them.
Plugin folders can exist in several generations (`design`, `design~g2`); the highest one is read. Folders starting
with a dot, such as `.staging`, are skipped.

## Scan pipeline

```mermaid
flowchart LR
    A["Discover sources<br/>global · plugins · projects · roots · synced"] --> B["Per source, in parallel<br/>skills/*/SKILL.md<br/>commands/**/*.md"]
    B --> C["Read the first 64 KB<br/>parse YAML frontmatter"]
    C --> D["Build Skill<br/>compute Health · lint"]
    D --> E["Tag · detect conflicts<br/>read skillOverrides"]
    E --> F["ScanResult<br/>immutable snapshot"]
```

- Sources are scanned with `Parallel.ForEachAsync`, one source per task.
- Only the head of each file is read during a scan. `Skill.Body` stays `null`; the card loads it on demand through
  `SkillBodyReader` (capped at 1 MB). Every read opens the file with shared read, write and delete access, so
  Lustrum never blocks an editor or Claude Code.
- A file or folder that cannot be read becomes a `ScanIssue`. It never aborts the scan.
- On one machine with about 300 skills a full scan takes about 0.2 s.

## Data model

Everything in `Lustrum.Core.Models` is an immutable `record`.

```csharp
SkillSource(Id, Kind, DisplayName, RootPath, LastSeen, Exists, IsEnabled, Namespace, Layout)   // ProjectFolder
Skill(Id, Name, FullName, Description, Kind, Source, Path, Body, Frontmatter,
      AllowedTools, Files, SizeBytes, Modified, Health, Tags) { Findings }
Conflict(Name, Skills)
ScanResult(Sources, Skills, Conflicts, Issues, ScannedAt, Duration) { Settings }
```

- `SourceKind`: `Managed`, `Global`, `Project`, `Plugin`, `Synced`, in Claude Code's order of precedence for plain
  names. `SkillKind`: `Skill`, `Command`.
- `SkillSource.Namespace` is the prefix of plugin and synced skills; `Layout` says whether skills sit in
  `skills/<name>/` or directly in the source folder.
- `Skill.Id` is `<source id>#<path relative to the source root>`. It is unique inside a scan and survives a
  frontmatter rename.
- `FullName` carries the prefix (`unity:ui`, `anthropic-skills:xlsx`). Conflicts are keyed by full name for prefixed
  skills and by short name otherwise, so `unity:ui` and a global `ui` do not collide, while two copies of
  `design:critique` (an installed and a synced plugin) do.
- `Skill.Tags` are computed: own-prefix tags, `plugin`, `synced`, `command`, `broken`, `duplicate`.
- `Skill.Findings` are the linter's `SkillFinding(Kind, Subject, Message)` records (see [Checks](#checks)).
- `ScanResult.Settings` holds the `skillOverrides` and `enabledPlugins` of managed, user, project and project-local
  settings (see [Visibility](#visibility)).

## Health

| Health | When | Counts as a problem |
|---|---|---|
| `Ok` | name and description present and valid | no |
| `DescriptionTooLong` | description longer than 1024 characters | yes, as a warning |
| `MissingDescription` | frontmatter has no `description` | yes |
| `MissingName` | frontmatter has no `name` (the folder name is used) | yes |
| `NoFrontmatter` | file does not start with `---` (folder name and first body line are used) | yes |
| `ParseError` | unclosed fence, invalid YAML or unreadable file | yes |

Commands are checked more leniently, because Claude Code does not require frontmatter for them: only `ParseError`
and `DescriptionTooLong` apply, and the name is always the file path.

The frontmatter parser strips a BOM, accepts CRLF, converts leading tabs to spaces, and takes `allowed-tools` as
either a YAML list or a comma-separated string (commas inside parentheses are kept, so `Bash(git:*, status)` stays
one entry). YAML errors are reported as `Line <n>: <cause>`, counted from the top of the file.

## Checks

Every skill whose frontmatter parses is linted while it is built, on the same 64 KB head the scanner already read;
the only extra I/O is one existence check per distinct link. Commands get the `allowed-tools` checks only, since they
have no folder of their own. Each finding becomes one entry in Problems and one row in the card's CHECKS section.

| Finding | Linter | When |
|---|---|---|
| `MissingFile` | `ReferenceLinter` | a markdown link (inline or reference style), or an inline-code path under `scripts/`, `references/` or `assets/`, points at a file that is not in the skill folder |
| `ToolSyntax` | `ToolLinter` | an `allowed-tools` entry is empty, has unbalanced parentheses, an empty pattern or no valid tool name |
| `UnknownTool` | `ToolLinter` | the name is not a known tool but is one edit away from one (two for names over four letters, a swap of neighbours counting as one) |
| `BroadTool` | `ToolLinter` | `Bash` or `PowerShell` without a command pattern, or with only a wildcard, which allows any shell command |

The linters are deliberately conservative, because a false alarm teaches people to ignore the list. The reference
linter skips fenced code (a fence closes only on its own marker and length), link syntax inside inline code, URLs and
other schemes, anchors, absolute and home paths, paths that leave the skill folder and paths with placeholders such as
`{name}`. Inline code counts as a path only under the three folders of the Agent Skills layout, or under a folder such
as `templates/` or `docs/` that the skill actually has, because those names are just as common in the project a skill
works on. Backslashes are read as folder separators. The tool linter reports a name only when it looks like a typo
of a known tool, so a tool Claude Code adds later passes, and `mcp__` tools always pass; `allowed-tools` may be a YAML
list or a comma- or space-separated string.

## Context budget

Claude Code lists every loaded skill to Claude in each session, by name and description with `when_to_use`
appended and the two capped at 1,536 characters, by name only for `name-only`, and not at all when hidden or when
`disable-model-invocation` is set; invoking a skill then adds its whole `SKILL.md`. `ContextBudget` counts those
characters for an effective set: active skills that are listed, skills on demand that join once Claude works below
their folder, and hidden ones. Shadowed copies cost nothing. Claude Code does not document the exact listing text and
no Claude tokenizer runs locally, so tokens are an estimate at four characters each and every label says `≈`. The
same numbers drive the Effective view's budget line and cost column, the card's CONTEXT section with the name-only
saving, and `lustrum list --budget`. They are counted before Claude Code fits the list into its own budget, 1% of the
context window by default, which drops the descriptions of the least-used skills first.

## Effective set and Missing

`ProjectContext.For(scan, project)` describes a session started in a project:

| Part | What it holds |
|---|---|
| loaded projects | the project and every project source in a parent folder up to the repository root |
| on-demand projects | project sources in folders below the project, with their folder relative to it |
| enabled plugins | plugins left on by managed, project-local, project and user settings, in that order |

`EffectiveSetBuilder.Build(scan, context)` takes managed and personal skills, the skills of the loaded projects, the
on-demand ones, enabled plugins and synced skills. Within one plain name it follows Claude Code's documented rules: a
skill beats a command, then managed beats personal beats project. Copies in the project and its parents all load, as
Claude Code loads them. The winner is `Active` and lists what it overrides; a loser is `Shadowed` and points at the
winner. Skills below the project are `OnDemand` and never compete for a name, because Claude Code invokes a clashing
one by its folder (`/apps/web:deploy`). Prefixed skills are never shadowed. Each skill also carries the visibility
that applies in the project, so a skill turned off there is labelled `off`.

`MissingSetBuilder.Build(scan, project)` is the complement: every skill on the machine the project does not load,
which means skills in other projects and skills of plugins that are off for this project. When a same-named copy is
loaded in the project anyway, the entry says where that copy comes from.

## Search and scopes

`SkillScope` is a small polymorphic hierarchy rather than a set of flags. Each scope decides for itself which skills
it includes.

| Scope | Includes |
|---|---|
| `AllScope` | everything |
| `KindScope(kind)` | all skills from managed, global, project, plugin or synced sources |
| `SourceScope(id)` | one project or one plugin |
| `MarketplaceScope(name)` | every plugin from one marketplace |
| `EffectiveScope(context)` | everything a `ProjectContext` loads, at start or on demand |
| `MissingScope(context)` | everything `EffectiveScope` leaves out |
| `ProblemsScope` | anything with a health problem, including `DescriptionTooLong` |

`SkillIndex.Search(skills, new SkillQuery(scope, text, requiredTags))` applies the scope and the tags, then the text.
Every word must match. Names are scored by `FuzzyMatcher`: exact, then prefix, then word start, then substring, then
an ordered subsequence (at least three characters, within three times the pattern length, so `rlsnotes` finds
`release-notes`). Descriptions and tags match as substrings only. With text, results are ordered by score.

With **+ SKILL.md** on, `SkillBodySearch` runs 250 ms after the last keystroke: it reads each `SKILL.md` body from
disk, keeps the ones that contain every word and appends them to the results. Bodies are not kept in memory.

## Usage

Claude Code writes every session to `~/.claude/projects/<encoded>/*.jsonl` (subagents included). A skill counts as
used by Claude when an assistant entry has a `Skill` tool call (`input.skill`), and as used by you when a user entry
contains `<command-name>/name</command-name>`. Usage is matched to skills by full name, so two skills with the same
name share their numbers.

Those files can add up to gigabytes, so `UsageCollector` reads them incrementally. Per file it remembers the length,
the modification time and the offset after the last complete line, and on the next pass reads only what was
appended. A hash of the first 4 KB catches files that were replaced rather than appended to. Lines are split on raw
bytes, and only lines that contain one of the two markers are decoded and parsed; lines over 16 MB are skipped. On
one machine with 1.4 GB of transcripts the first pass took 0.3 s and the next one 7 ms.

`SkillUsage` keeps the count, the split between Claude and you, the projects, the last use and the time of every
use; `PerDay` turns those into the card's 30-day strip in the local time zone. The window collects usage in the
background after the first scan and on every manual rescan; watcher rescans leave it alone. Until usage is known, the
`unused` chip is disabled, and it stays disabled when no transcripts exist.

## Compare

`SkillComparer` runs a line diff (DiffPlex) over the whole files, frontmatter included. Line endings, a byte order
mark and trailing blank lines are ignored. `DiffFolding` keeps three lines of context around each change and folds
longer unchanged runs; the card shows at most 2,000 rows. `−` is only in the selected copy, `+` only in the other one.
The CLI prints the same diff in unified format.

## Git

Only global and project skills are considered. Plugin and synced folders are managed by Claude Code and the Claude
desktop app.

`GitRepositoryLocator` finds a skill's repository by walking up from its folder to the nearest `.git` entry (a
folder, or a file for worktrees and submodules), without starting git. Symbolic links are resolved first, so a
`skills` folder linked into a dotfiles checkout is matched against that checkout.

| Role | When | What Lustrum does |
|---|---|---|
| `SkillsOnly` | the repository root is the skill folder itself, or lies inside a `skills` or `commands` folder | status, checks and updates |
| `Containing` | anything larger: a project, `~/.claude`, a dotfiles repository | status and checks only |

The card reads local state with the git command line: `symbolic-ref` and `rev-list --left-right --count HEAD...@{u}`
for branch, upstream, behind and ahead; `status --porcelain=v1 -z` for uncommitted changes (only under the skill
folder in a `Containing` repository); `log -1` for the last commit that touched the skill. Behind and ahead are
relative to the upstream as of the last fetch, and the card says so until a check runs.

Nothing goes over the network until **Check for updates**, which runs `git fetch` for a repository with an upstream.
The header button checks every repository that holds skills, project repositories included, four at a time. The
`updates` chip then keeps the skills of skills repositories that are behind; a project repository's count stays on the
card, because commits elsewhere in the project say nothing about the skill. Per-repository state
(`RepoState`: not checked, checking, updating, checked, failed) lasts for the session and survives rescans; a
repository that is busy is not checked again.

`UpdatePolicy` returns `UpdateAvailable`, `UpToDate` or `UpdateBlocked(reason)`. An update needs a `SkillsOnly`
repository on a branch with an upstream, behind it, with no uncommitted changes and no local commits.
`SkillGit.UpdateAsync` fetches, evaluates the policy again on fresh state, refuses when the upstream starts tracking a
file that exists locally as an ignored file (git would replace it silently), and only then runs
`git merge --ff-only @{u}`.

git runs without a terminal: `GIT_TERMINAL_PROMPT=0` and `SSH_ASKPASS_REQUIRE=never` turn a password prompt into a
failed check, `LC_ALL=C` keeps messages in English, `GIT_OPTIONAL_LOCKS=0` stops `status` from rewriting the index,
and `core.fsmonitor` is off so reading never starts a monitor daemon. Local commands time out after 30 s, fetches
after 90 s, and a process that outlives its timeout is killed with its children.

## Visibility

Claude Code reads a `skillOverrides` map from its settings files: skill name to `on`, `name-only`,
`user-invocable-only` or `off`. `SettingsLayersReader` reads it, with `enabledPlugins`, from the managed settings,
from `~/.claude/settings.json` and from each project's `.claude/settings.json` and `.claude/settings.local.json`.
Managed wins over project-local, which wins over project, which wins over user settings, key by key.

The managed layer is chosen the way Claude Code ranks its admin sources:

| Rank | Source | Read from |
|---|---|---|
| 1 | server-managed settings (claude.ai console or a gateway) | Claude Code's cache, `~/.claude/remote-settings.json` |
| 2 | device policy | macOS: the `com.anthropic.claudecode` configuration profile in `/Library/Managed Preferences` (device-wide, then the user's), converted with `plutil`; Windows: the `Settings` value under `HKLM\SOFTWARE\Policies\ClaudeCode` |
| 3 | managed files | `managed-settings.json`, then every `*.json` in `managed-settings.d/` in name order, each overriding the last |
| fallback | user policy | Windows: the `Settings` value under `HKCU\SOFTWARE\Policies\ClaudeCode`, only when none of the above is present |

The first source with a policy key supplies the policy and the rest are skipped. When the highest present source sets
`managedSourcesBehavior` to `"merge"`, every admin source contributes, higher ones winning key by key. A source that
exists but cannot be read is reported in Problems and still counts as present. OS policies are read only through an
`IPolicyStore` the caller passes: the window and the CLI pass `SystemPolicyStore`, tests a fake one. A converted
profile is kept until its file changes, and `/Library/Managed Preferences` is watched like the skill folders.

`skillOverrides` does not affect plugin skills or commands, as in Claude Code.

| Value | Claude sees | `/name` in the menu | Label in Lustrum |
|---|---|---|---|
| `on` (or no entry) | name and description | yes | none |
| `name-only` | the name only | yes | `name only` |
| `user-invocable-only` | nothing | yes | `you only` |
| `off` | nothing | no | `off`, row dimmed |

The card's control writes one entry: to `~/.claude/settings.json` for a personal skill, and to the project's
`.claude/settings.local.json` for a project skill, the file Claude Code's own `/skills` menu writes. In a project view,
a skill inherited from a parent or nested folder is written to the session project's `settings.local.json`, because
only the settings of the folder a session starts in apply there. Managed skills and
managed settings are never written; when a managed entry decides a skill's visibility, the card says so. Choosing **On**
removes the entry, unless a lower file would then hide the skill again; then an explicit `on` is written.

`ClaudeSettingsWriter` changes only that entry and keeps every other key and character. It follows a linked settings
file to the real one, writes a temporary file next to it with the same file mode, flushes it to disk and moves it over
the original. If the file changed while Lustrum was writing, the change is refused. A file that is not plain JSON
(comments, trailing commas) is left alone. Writes are serialised, and the control is disabled while one is running.

## Copy

**Copy to** lists your personal skills and every existing project other than the skill's own source. `SkillCopier`
plans each target and returns `ReadyCopy` or `BlockedCopy(reason)`: a target is refused when the folder or file
already exists there or when it already has a skill or command with that name. A skill folder is copied without
`.git` and without linked files or folders, into a staging folder next to `skills/` (where neither Lustrum nor Claude
Code looks for skills), and moved into place at the end, so a failure or a rescan never sees half a skill. A command
keeps its relative path under `commands/`. Plugin and synced skills can be copied out to customise them. Copies over
2,000 files or 50 MB are refused while the folder is still being walked.

A project copy of a personal skill is shadowed by the personal one in that project; copying into a project is for
sharing a skill with the team through the repository.

## Live updates

`WatchTargets` picks what to watch. Skill and command folders (including those of synced plugins) and the plugin
cache are watched recursively. `~/.claude/plugins`, `~/.claude/projects` and each project's `.claude` are watched
without subfolders, and `~/.claude` itself only for `settings.json`, `skills` and `commands`, because Claude Code
writes transcripts, history and worktrees there constantly. Events inside folders whose name starts with a dot
(`.git`, `.staging`, `.DS_Store`) are ignored.

`SkillTreeWatcher` debounces events by 500 ms. It falls back to polling a fingerprint every 30 s when there are more
folders than the platform handles comfortably (64 on Linux, where each watcher is an inotify instance, 256 elsewhere)
or when native watchers cannot be created. The fingerprint uses names, file sizes and file times, not folder times,
which Windows updates lazily. The watcher is built off the UI thread, and the CLI line says which mode is active.

## Threading and failures

- The scan runs on the thread pool; `MainViewModel` applies the result on the UI thread.
- Starting a new rescan cancels the previous one, and a result from a superseded scan is dropped.
- git runs as a child process with asynchronous I/O; loading a card's git state is cancelled when the card changes.
- Failures from the scan, the content search, git, the writes, the editor or the file manager are shown on the CLI
  line; none of them closes the window.

## The window

The layout is a Swiss grid: a source column, a numbered register and a card, under a header and above a
terminal-style CLI line that shows the `lustrum` command for the current view. Type is Archivo (regular,
semi-condensed, condensed and extra-condensed cuts) and JetBrains Mono, bundled under the OFL. Each cut is loaded from
its own file, because Avalonia's font collection does not tell the width variants of one family apart. Light and dark
themes share one set of semantic colours with a single lime accent. The census-grid mark is drawn by the
`LustrumMark` control, and the app icons are rendered from the same geometry.

`MainViewModel` is split into partial files by concern: list and sorting, content search, project views, usage, git,
writes, footer, problems, card, commands, settings. States are types rather than flags:

| Type | Cases |
|---|---|
| `ProjectView` | `Own`, `Effective`, `Missing` |
| `UsageState` | loading, ready, failed; each describes a skill's usage in its own words |
| `GitCardState` | hidden, loading, not in a repository, failed, ready |
| `RepoState` | not checked, checking, updating, checked, failed |
| `ChipFilter` | tag, unused, updates; each decides when it is available |

| Piece | Role |
|---|---|
| `NavTreeBuilder`, `NavSection` | `ScanResult` to the source column, with counts and jump keys |
| `SkillItemViewModel`, `ListHeader` | one register row with its number, state and mark; a group heading |
| `SkillCardViewModel` | the card; loads the `SKILL.md` body and the git state in the background |
| `GitSection`, `VisibilityPanel` | what the card's git and visibility sections show |
| `KeyRouter` | maps key presses to shortcuts; ignores keys from popups and text boxes |
| `IShellService`, `IClipboardService`, `ISettingsService`, `IThemeService`, `ISkillGit`, `ISkillWriter` | the only places that touch the OS; faked in tests |

## Testing

- Core tests never read the real `~/.claude`. `FakeClaudeHome` builds a throw-away home with skills, commands, a
  plugin registry, settings, project folders and `~/.claude.json`, and the scanner runs against it: colliding project
  keys, malformed registries, unreadable folders, odd folder names, BOMs and tabs in frontmatter, native and polling
  watching.
- Git tests run the real git command line against `GitSandbox`: throw-away repositories with a bare remote, so
  behind, ahead, dirty trees, detached heads, ignored-file collisions and refused updates are real git states. The
  sandbox hides the machine's global and system git configuration.
- Linter tests run against real skill folders: present and missing links, fenced code, escapes, placeholders, and
  `allowed-tools` syntax, suggestions and broad entries.
- Writer tests cover symlinked settings files, file modes, escaping and refusals; copier tests
  cover staging, links and limits.
- App tests drive `MainViewModel` with fake scan, usage, shell, clipboard, settings, theme, git and writer services,
  including overlapping rescans, in-flight writes and checks, and key routing.
- CI runs the whole suite on Ubuntu, macOS and Windows for every pull request; `main` accepts only green changes.
