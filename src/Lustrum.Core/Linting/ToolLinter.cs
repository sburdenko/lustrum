namespace Lustrum.Core.Linting;

/// <summary>
/// Checks allowed-tools entries: their syntax, misspelt tool names, and whether a shell is allowed without a command
/// pattern. Claude Code keeps adding tools, so a name is only reported when it is one or two letters away from a tool
/// Lustrum knows; anything else, and every MCP tool, passes.
/// </summary>
public static class ToolLinter
{
    public static readonly IReadOnlySet<string> KnownTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "Agent", "AskUserQuestion", "Bash", "BashOutput", "CronCreate", "CronDelete", "CronList", "Edit", "EnterPlanMode",
        "EnterWorktree", "ExitPlanMode", "ExitWorktree", "Glob", "Grep", "KillShell", "LS", "LSP", "ListMcpResourcesTool",
        "Monitor", "MultiEdit", "NotebookEdit", "NotebookRead", "PowerShell", "PushNotification", "Read",
        "ReadMcpResourceTool", "RemoteTrigger", "ScheduleWakeup", "SendMessage", "Skill", "SlashCommand", "Task",
        "TaskCreate", "TaskGet", "TaskList", "TaskOutput", "TaskStop", "TaskUpdate", "TeamCreate", "TeamDelete", "TodoRead",
        "TodoWrite", "ToolSearch", "WebFetch", "WebSearch", "Workflow", "Write",
    };

    private static readonly HashSet<string> Shells = new(StringComparer.Ordinal) { "Bash", "PowerShell" };

    private static readonly HashSet<string> OpenPatterns = new(StringComparer.Ordinal) { "*", "**", "*:*", ":*" };

    public static IReadOnlyList<SkillFinding> Lint(IEnumerable<string> entries)
    {
        var findings = new List<SkillFinding>();
        foreach (var raw in entries)
        {
            var entry = raw.Trim();
            if (Check(entry) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static SkillFinding? Check(string entry)
    {
        if (entry.Length == 0)
        {
            return Syntax(entry, "allowed-tools has an empty entry");
        }

        var open = entry.IndexOf('(');
        var close = entry.LastIndexOf(')');
        if (entry.Count(c => c == '(') != entry.Count(c => c == ')') || (open >= 0 && close != entry.Length - 1) || (open < 0 && close >= 0))
        {
            return Syntax(entry, $"allowed-tools entry {entry} has unbalanced parentheses");
        }

        var name = open < 0 ? entry : entry[..open].Trim();
        var pattern = open < 0 ? null : entry[(open + 1)..close].Trim();
        if (name.Length == 0 || name.Any(char.IsWhiteSpace))
        {
            return Syntax(entry, $"allowed-tools entry {entry} has no valid tool name");
        }

        if (pattern is { Length: 0 })
        {
            return Syntax(entry, $"allowed-tools entry {entry} has an empty pattern");
        }

        if (!name.StartsWith("mcp__", StringComparison.Ordinal) && !KnownTools.Contains(name) && Closest(name) is { } guess)
        {
            return new SkillFinding(FindingKind.UnknownTool, entry, $"allowed-tools names {name}, which is not a Claude Code tool; did you mean {guess}?");
        }

        return Shells.Contains(name) && (pattern is null || OpenPatterns.Contains(pattern))
            ? new SkillFinding(FindingKind.BroadTool, entry, $"allowed-tools entry {entry} lets the skill run any shell command without asking")
            : null;
    }

    private static SkillFinding Syntax(string entry, string message) => new(FindingKind.ToolSyntax, entry, message);

    private static string? Closest(string name) =>
        KnownTools
            .Select(tool => (Tool: tool, Distance: Distance(name.ToLowerInvariant(), tool.ToLowerInvariant())))
            .Where(p => p.Distance <= (name.Length <= 4 ? 1 : 2))
            .OrderBy(p => p.Distance)
            .Select(p => p.Tool)
            .FirstOrDefault();

    /// <summary>Edit distance where swapping two neighbouring letters counts as one edit.</summary>
    private static int Distance(string a, string b)
    {
        var beforePrevious = new int[b.Length + 1];
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    current[j] = Math.Min(current[j], beforePrevious[j - 2] + 1);
                }
            }

            beforePrevious = previous;
            previous = current;
        }

        return previous[b.Length];
    }
}
