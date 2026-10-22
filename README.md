<div align="center">

<img src="src/Lustrum.App/Assets/lustrum.png" alt="" width="104"/>

# Lustrum

**A census of every Claude Code skill on your machine.**

Global, project, plugin and claude.ai-synced skills and slash commands in one keyboard-first window,<br/>
with the same data in a CLI. Nothing changes on its own: Lustrum writes only when you ask it to.

[![CI](https://github.com/sburdenko/lustrum/actions/workflows/ci.yml/badge.svg)](https://github.com/sburdenko/lustrum/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Avalonia 11.3](https://img.shields.io/badge/Avalonia-11.3-8B5CF6)
![macOS · Windows · Linux](https://img.shields.io/badge/macOS%20·%20Windows%20·%20Linux-0E0E0C)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/overview-dark.png">
  <img src="docs/overview.png" alt="Lustrum: source column with counts, numbered skill register grouped by kind, skill card with usage and actions, CLI line at the bottom" width="920"/>
</picture>

</div>

## Why

Claude Code spreads skills over several places. `/plugin` shows plugins, `/skills` shows the current project, and
`npx skills list` shows the current folder plus home. Nothing answers the questions that come up once you have a few
dozen skills:

- What skills do I have, and where does each one live?
- Which ones does project X actually load, and which copy wins when names collide?
- Which ones are broken, and which are never used?
- Which ones came from git, and has the upstream moved on since?

Lustrum scans the disk into an immutable snapshot, derives every view from it, and keeps itself up to date while you
edit skills.

## What it does

| | |
|---|---|
| **Inventory** | Every skill and command from managed, personal, project, plugin and claude.ai sources, with counts per source and per marketplace. Fuzzy search on names, substring on descriptions and tags, optionally the text of every `SKILL.md`. |
| **Project views** | Pick a project and switch between **Own** (its skills), **Effective** (exactly what Claude Code loads there: its own skills and those of parent folders up to the repository root, folders below it on demand, managed and personal skills, and the plugins its settings leave on, resolved by Claude Code's precedence rules) and **Missing** (skills on this machine it does not load). |
| **Register** | A numbered list grouped by kind, with the state of each skill (shadowed, off, behind) and a mark for errors, long descriptions, failed checks and duplicate names. Filter chips combine: own prefixes, `plugin`, `synced`, `command`, `broken`, `duplicate`, `unused`, `updates`. |
| **Health** | One Problems list: broken frontmatter with the YAML line and cause, over-long descriptions, projects whose folder is gone, paths that could not be read. |
| **Checks** | Each `SKILL.md` is linted while it is read: links and `scripts/`, `references/` or `assets/` paths to files that are not in the skill folder, malformed `allowed-tools` entries, misspelt tool names with the likely meant one, and a bare `Bash` or `PowerShell` that allows any shell command. Findings show on the card and in Problems. |
| **Context budget** | In a project's Effective view, an estimate of how many tokens the skill list takes in every session, per skill and in total, by Claude Code's rules: description plus `when_to_use`, capped at 1,536 characters, name only for `name-only`, nothing for hidden skills and `disable-model-invocation`. Skills sort by cost on request; the card shows what a skill costs listed and invoked, and what name-only would save. |
| **Usage** | How often each skill was used, by Claude or by you, when last and in which projects, with a 30-day strip, read incrementally from Claude Code transcripts. |
| **Compare** | A line diff between copies of one name, with unchanged runs folded. |
| **Git** | Branch, upstream, behind and ahead, uncommitted changes and last commit for skills in a git repository. **Check for updates** fetches; **Update** fast-forwards a repository that holds only skills. |
| **Visibility** | Turn a skill on, name-only, user-only or off through Claude Code's own `skillOverrides` setting, the switch behind its `/skills` menu. |
| **Copy to** | Copy a skill into your personal skills or a project, for example to share it with a team or to customise a plugin skill. Nothing is overwritten. |
| **CLI line** | The bar at the bottom shows the `lustrum` command that prints the current view, such as `list --project ~/work/storefront --missing` or `diff release-notes`. |
| **Live** | File system watchers rescan half a second after a skill or a plugin setting changes. |

<table>
<tr>
<td width="50%"><img src="docs/effective.png" alt="Effective set for a project: the personal copy shadows the project copy, one skill is turned off"/></td>
<td width="50%"><img src="docs/missing.png" alt="Missing view: skills from other projects that this project does not load"/></td>
</tr>
<tr>
<td align="center"><sub><b>Effective</b> · what a project loads; the personal copy wins</sub></td>
<td align="center"><sub><b>Missing</b> · what other projects have and this one does not</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/problems.png" alt="Problems list: invalid YAML, missing frontmatter, long description, dead links, allowed-tools issues, missing project"/></td>
<td width="50%"><img src="docs/checks.png" alt="Effective view with the token budget and a cost column; the card lists two dead links and the context a skill takes"/></td>
</tr>
<tr>
<td align="center"><sub><b>Problems</b> · every broken thing in one list</sub></td>
<td align="center"><sub><b>Checks · Context</b> · dead links and what a skill costs in context</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/compare.png" alt="Line diff between a project copy and the personal copy of a skill"/></td>
<td width="50%"><img src="docs/git.png" alt="A cloned skill two commits behind its upstream with Update enabled and the updates chip on"/></td>
</tr>
<tr>
<td align="center"><sub><b>Compare</b> · two copies of one name, line by line</sub></td>
<td align="center"><sub><b>Git</b> · a cloned skill two commits behind</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/visibility.png" alt="A personal skill turned off by project settings, with the Copy to menu open"/></td>
<td width="50%"><img src="docs/content.png" alt="Rendered SKILL.md in the dark theme"/></td>
</tr>
<tr>
<td align="center"><sub><b>Visibility · Copy to</b> · project settings turn a skill off</sub></td>
<td align="center"><sub><b>Content</b> · rendered <code>SKILL.md</code>, dark theme</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/settings.png" alt="Settings panel in the dark theme"/></td>
<td width="50%"></td>
</tr>
<tr>
<td align="center"><sub><b>Settings</b> · prefixes, roots, editor, theme</sub></td>
<td></td>
</tr>
</table>

<sub>Screenshots come from a synthetic `~/.claude`, rendered headless by `tools/Lustrum.Screenshots`.</sub>

## Keyboard

| Key | Action | Key | Action |
|---|---|---|---|
| `j` `k` | move through the list | `/` | search |
| `⏎` | open in editor | `O` | show in folder |
| `C` | copy to… | `D` | compare copies |
| `U` | check all repositories for updates | `A` `M` `G` `P` `L` `S` `!` | jump to All, Managed, Global, Projects, Plugins, claude.ai, Problems |
| `Esc` | clear search | `F5` | rescan |
| `Cmd/Ctrl+C` | copy the invocation name | `Cmd/Ctrl+Shift+C` | copy the path |
| `Cmd/Ctrl+,` | settings | `Cmd/Ctrl+1/2/3` | focus a column |

## Get it

**Prebuilt.** Every `v*` tag publishes self-contained builds to
[Releases](https://github.com/sburdenko/lustrum/releases); no .NET install is needed.

| Platform | Download | Contents |
|---|---|---|
| macOS (Apple silicon, Intel) | `lustrum-<tag>-osx-arm64.zip`, `lustrum-<tag>-osx-x64.zip` | `Lustrum.app` and `cli/lustrum` |
| Linux x64 | `Lustrum-<tag>-x86_64.AppImage`, `lustrum-<tag>-linux-x64.tar.gz` | AppImage of the app; archive with the app and the CLI |
| Windows x64 | `lustrum-<tag>-win-x64.zip` | `Lustrum.exe` and `cli\lustrum.exe` |

The macOS app is ad-hoc signed but not notarized, so Gatekeeper blocks it until you clear the quarantine flag:

```bash
xattr -dr com.apple.quarantine Lustrum.app
```

**From source.** You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/sburdenko/lustrum.git && cd lustrum
dotnet run --project src/Lustrum.App               # the window
dotnet run --project src/Lustrum.Cli -- list       # the same data in a terminal
dotnet test                                        # 498 tests
```

## Architecture

```mermaid
flowchart TB
    subgraph front["Front ends"]
        direction LR
        app["<b>Lustrum.App</b><br/>Avalonia 11 · MVVM"]
        cli["<b>Lustrum.Cli</b><br/>list · diff"]
    end

    subgraph core["Lustrum.Core · no UI dependency"]
        direction TB
        read["<b>Read</b><br/>Scanning · Parsing · Usage · Watching"]
        derive["<b>Derive</b><br/>Indexing · Effective · Problems · Comparing"]
        act["<b>Act on request</b><br/>Git · ClaudeSettings · Copying"]
        read --> derive --> act
    end

    disk[("~/.claude · ~/.claude.json · &lt;project&gt;/.claude")]
    gitcli[["git"]]

    front --> core
    read --> disk
    act --> disk
    act --> gitcli
```

**Principles**

- **One snapshot.** A scan produces an immutable `ScanResult`. Every list, count, effective set and problem is derived
  from it, and the window and the CLI share the same code.
- **Core owns the rules.** Discovery, Claude Code's precedence rules, health, usage, git and settings handling live in
  `Lustrum.Core`, which has no UI dependency. Front ends only present.
- **States are types, not flags.** Scopes, project views, usage, git state, update decisions and copy plans are small
  record hierarchies; each case carries exactly the data it needs and knows how to describe itself.
- **Read-only until asked.** Three writes exist, each behind an explicit control, each re-checking its preconditions
  on fresh state right before it writes.
- **Failures are data.** An unreadable file becomes a `ScanIssue` and shows up in Problems; nothing aborts a scan, and
  no error closes the window.
- **The OS sits behind interfaces.** Shell, clipboard, settings, theme, git and writes are services, faked in view
  model tests; Core tests run against throw-away homes and real git repositories.

```mermaid
sequenceDiagram
    autonumber
    participant UI as Window
    participant Core as SkillScanner
    participant Disk as ~/.claude and projects
    participant Usage as UsageCollector
    participant Watch as SkillTreeWatcher
    UI->>Core: ScanAsync(paths, options)
    Core->>Disk: discover sources, read file heads in parallel
    Core-->>UI: ScanResult (immutable)
    UI->>UI: derive register, card, effective set, problems
    UI->>Usage: CollectAsync (background)
    Usage->>Disk: read only what was appended since last time
    Usage-->>UI: UsageReport
    Disk-->>Watch: file events
    Watch-->>UI: debounced 500 ms, then rescan
```

**What Lustrum writes, and the guarantees**

| Action | Writes | Before writing it checks | How it writes |
|---|---|---|---|
| **Visibility** | one `skillOverrides` entry in `~/.claude/settings.json` (personal skill) or `<project>/.claude/settings.local.json` (project skill) | the file is plain JSON and an object; it has not changed since it was read | every other key and character kept; follows links; temporary file with the same mode, flushed, then moved over |
| **Copy to** | a new skill folder or command file in personal skills or a project | nothing with that folder name or skill name exists in the target; under 2,000 files and 50 MB | without `.git` and linked files; staged outside `skills/`, moved into place at the end |
| **Update** | the working tree of a git repository that holds only skills | fresh fetch; on a branch with an upstream; behind; clean tree; no local commits; no ignored file would be replaced | `git merge --ff-only @{u}`, never a merge commit |

Network access happens only on **Check for updates** and **Update**.

| Core module | Responsibility | Key types |
|---|---|---|
| `Scanning` · `Parsing` | find sources, read skills, parse frontmatter, decode project folder names | `SkillScanner`, `SourceDiscovery`, `FrontmatterParser`, `ProjectPathDecoder` |
| `Indexing` | scopes and ranked search | `SkillScope`, `SkillIndex`, `FuzzyMatcher` |
| `Effective` | what a project loads (parents, on-demand folders, per-project plugins) and what it misses, by Claude Code's rules | `ProjectContext`, `EffectiveSetBuilder`, `MissingSetBuilder` |
| `Linting` | dead links and resource paths in `SKILL.md`, `allowed-tools` syntax, unknown and over-broad tools | `ReferenceLinter`, `ToolLinter`, `SkillFinding` |
| `Budget` | characters and estimated tokens a skill takes in Claude's skill list | `ContextBudget`, `BudgetSummary` |
| `Problems` · `Comparing` | health problems; folded line diffs | `ProblemCollector`, `SkillComparer` |
| `Usage` | incremental transcript reading | `UsageCollector`, `TranscriptParser`, `SkillUsage` |
| `Git` | repository roles, status, fetch, update policy | `GitRepositoryLocator`, `SkillGit`, `UpdatePolicy`, `GitCli` |
| `ClaudeSettings` | `skillOverrides` and `enabledPlugins` from every managed source (server cache, configuration profile or registry, files), user, project and local settings; safe edits | `SettingsLayers`, `SettingsLayersReader`, `SystemPolicyStore`, `ClaudeSettingsWriter` |
| `Copying` | planned, staged copies | `SkillCopier`, `CopyPlan` |
| `Watching` | what to watch, debouncing, polling fallback | `WatchTargets`, `SkillTreeWatcher` |

The full design notes, from discovery and the data model to the git and write paths, threading and the window, are in
**[docs/architecture.md](docs/architecture.md)**. The Claude Code files Lustrum reads are described in
[docs/claude-code-layout.md](docs/claude-code-layout.md).

## CLI

```text
lustrum list [--json] [--source managed|global|project|plugin|synced] [--broken] [--project <folder> [--missing|--budget]] [--usage] [--unused]
lustrum problems [--json]
lustrum diff <name>
```

```text
$ lustrum list --project ~/work/storefront --source project --budget
NAME           SOURCE      HEALTH  IN PROJECT              ≈TOKENS  DESCRIPTION
ui-kit         storefront  ok      loaded                  21       Use the shared component library correctly: tokens, spacing, accessibil…
checkout-flow  storefront  ok      loaded                  19       Trace the checkout flow and find where an order can get stuck.
admin-reports  admin       ok      on demand · apps/admin  18       Build the weekly admin reports from the orders database.
release-notes  storefront  ok      shadowed by Global      0        Release notes for the storefront: customer-facing wording, no ticket id…

≈356 tokens in Claude's skill list from 20 listed skills, 2 hidden; 1 more on demand, ≈18 tokens. Estimate: about four characters per token.

$ lustrum list --broken
NAME           SOURCE  HEALTH              DESCRIPTION
brand-voice    Global  DescriptionTooLong  Rewrite copy in the house tone of voice. Applies the style guide, banne…
broken-yaml    Global  ParseError          (no description)
legacy-import  Global  NoFrontmatter       Imports old markdown notes.

$ lustrum problems
KIND    SUBJECT            MESSAGE
broken  broken-yaml        ParseError: Line 2: syntax error, check brackets, quotes and colons
broken  legacy-import      NoFrontmatter
long    brand-voice        Description is over 1024 characters (1660 characters)
link    pdf-forms          Links to scripts/fields.py, which is not in the skill folder
link    pdf-forms          Links to references/fields.md, which is not in the skill folder
tool    sql-helper         allowed-tools names Grpe, which is not a Claude Code tool; did you mean Grep?
wide    sql-helper         allowed-tools entry Bash lets the skill run any shell command without asking
gone    retired-prototype  Project folder no longer exists: /Users/you/work/retired-prototype

8 problems
```

- `--project <folder>` prints that project's effective set with an `IN PROJECT` column; `--missing` lists what it
  does not load, with a `NOTE` for disabled plugins and names loaded from elsewhere; `--budget` adds an estimated
  `≈TOKENS` column, sorts by it and ends with the total for the whole skill list, whatever the filters.
- `problems` prints the Problems list and exits with 1 when there is anything in it, so it can run in CI or a
  pre-commit hook.
- `--usage` adds `USES` and `LAST USED`; `--unused` keeps skills with no recorded use and refuses to run when there are
  no transcripts at all.
- `--json` prints full records: frontmatter, allowed tools, files, health, tags, and with `--project` the
  availability, `shadowedBy`, `overrides`, `visibility` and `listedChars`.
- `diff <name>` prints every copy of a name against the project copy in unified format.
- Unreadable files go to stderr as `warning: <path>: <reason>`. Exit code 2 means bad input (unknown source, a folder
  without `.claude`, an unknown name); exit code 1 means a copy could not be read, or for `problems`, that there is at
  least one problem.

## Quality

| | |
|---|---|
| **Tests** | 498 xUnit tests, about 5,200 lines, more than the Core code they cover |
| **Coverage** | 88% of lines in `Lustrum.Core`, 87% in the view models (coverlet) |
| **Real environments** | scanner tests on throw-away home folders; git tests on real repositories with a bare remote; writer tests on real files, links and file modes |
| **CI** | the full suite on Ubuntu, macOS and Windows for every pull request; `main` only accepts green changes |
| **Builds** | warnings are errors; self-contained single-file releases for macOS (arm64, x64), Linux x64 and Windows x64 |
| **Size** | Core about 3,700 lines of C#, the window about 2,900 lines of C# and 1,200 of XAML, the CLI about 420 |

```text
lustrum/
├── src/
│   ├── Lustrum.Core/        Scanning/ Parsing/ Indexing/ Effective/ Linting/ Budget/ Problems/ Comparing/ Usage/ Git/ ClaudeSettings/ Copying/ Watching/ Settings/ Models/
│   ├── Lustrum.App/         Views/ ViewModels/ Services/ Controls/ Assets/
│   └── Lustrum.Cli/         list and diff commands, table renderer
├── tests/                   Core, App and CLI test projects
├── tools/Lustrum.Screenshots/   headless renderer for the screenshots above
├── docs/                    architecture, Claude Code layout, screenshots
├── build/                   macOS app bundle and Linux AppImage packaging
└── .github/workflows/       CI on three OSes, release builds on tags
```

## Settings and logs

Settings live in `lustrum/settings.json` under the user's application-data folder (`~/Library/Application Support`
on macOS, `~/.config` on Linux, `%APPDATA%` on Windows) and are written only on **Apply and rescan**. Logs go to
`lustrum/logs/`, kept for 7 days: scan size and duration, missing sources, read issues and time to first data.

## Limitations

- Only files on disk are visible. claude.ai skills appear only where the Claude desktop app has synced them, and the
  synced layout is not documented by Anthropic; nor is the transcript format usage is read from.
- Server-managed settings are read from Claude Code's local cache, `~/.claude/remote-settings.json`; Lustrum never
  fetches them itself, so they are as fresh as Claude Code's last fetch.
- Folders below a project are searched to the configured depth (default 4). Directories added with `--add-dir` are
  chosen per session on the command line and stored nowhere, so they cannot be modelled.
- Token counts are estimates at about four characters per token; Claude Code does not publish the exact text of its
  skill list or the tokenizer. They are counted before Claude Code fits the list into its own budget (1% of the
  context window by default), which drops descriptions of the least-used skills when the list is too long, so a large
  total means descriptions are being cut rather than that many tokens being spent.
- Only the first 64 KB of a `SKILL.md` is linted. Misspelt tool names are found against Lustrum's own list of Claude
  Code tools; a name far from every tool on it is not reported.
- Claude Code's documentation does not say how `skillOverrides` from several files combine; Lustrum assumes key by key,
  the higher file winning.
- Git features need `git` on the `PATH`; a remote that asks for a password fails the check instead of prompting.
  **Update** only fast-forwards and runs the repository's own hooks, as `git merge` would.
- The window is developed on macOS; on Windows and Linux the test suite runs in CI. The macOS app is not notarized, and
  there is no Windows installer.

## Built with

[Avalonia](https://avaloniaui.net/) · [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) ·
[YamlDotNet](https://github.com/aaubry/YamlDotNet) · [DiffPlex](https://github.com/mmanela/diffplex) ·
[Markdown.Avalonia](https://github.com/whistyun/Markdown.Avalonia) · [System.CommandLine](https://github.com/dotnet/command-line-api) ·
[Serilog](https://serilog.net/) · [FluentIcons](https://github.com/davidxuang/FluentIcons) · xUnit · FluentAssertions

Type: [Archivo](https://github.com/Omnibus-Type/Archivo) and [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono),
both under the SIL Open Font License; the licence texts sit next to the font files.

## Name

A *lustrum* was the Roman census, taken every five years: count everyone, then purify the books.
