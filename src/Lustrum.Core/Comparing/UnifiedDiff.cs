using System.Text;

namespace Lustrum.Core.Comparing;

public static class UnifiedDiff
{
    private const int Context = 3;

    public static string Format(SkillDiff diff, string leftLabel, string rightLabel)
    {
        if (diff.IsIdentical)
        {
            return "Files are identical.";
        }

        var builder = new StringBuilder();
        builder.Append("--- ").Append(leftLabel).Append('\n');
        builder.Append("+++ ").Append(rightLabel).Append('\n');
        foreach (var (start, end) in Hunks(diff.Lines))
        {
            AppendHunk(builder, diff.Lines, start, end);
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static IEnumerable<(int Start, int End)> Hunks(IReadOnlyList<DiffLine> lines)
    {
        var changes = Enumerable.Range(0, lines.Count).Where(i => lines[i].Kind != DiffKind.Same).ToList();
        var start = Math.Max(0, changes[0] - Context);
        var end = Math.Min(lines.Count, changes[0] + Context + 1);
        foreach (var change in changes.Skip(1))
        {
            if (change - Context <= end)
            {
                end = Math.Min(lines.Count, change + Context + 1);
                continue;
            }

            yield return (start, end);
            start = change - Context;
            end = Math.Min(lines.Count, change + Context + 1);
        }

        yield return (start, end);
    }

    private static void AppendHunk(StringBuilder builder, IReadOnlyList<DiffLine> lines, int start, int end)
    {
        var hunk = lines.Skip(start).Take(end - start).ToList();
        var leftCount = hunk.Count(l => l.LeftNumber is not null);
        var rightCount = hunk.Count(l => l.RightNumber is not null);
        var leftStart = hunk.FirstOrDefault(l => l.LeftNumber is not null)?.LeftNumber ?? PreviousNumber(lines, start, l => l.LeftNumber);
        var rightStart = hunk.FirstOrDefault(l => l.RightNumber is not null)?.RightNumber ?? PreviousNumber(lines, start, l => l.RightNumber);
        builder.Append($"@@ -{leftStart},{leftCount} +{rightStart},{rightCount} @@\n");
        foreach (var line in hunk)
        {
            var prefix = line.Kind switch
            {
                DiffKind.Added => '+',
                DiffKind.Removed => '-',
                _ => ' ',
            };
            builder.Append(prefix).Append(line.Text).Append('\n');
        }
    }

    private static int PreviousNumber(IReadOnlyList<DiffLine> lines, int before, Func<DiffLine, int?> number) =>
        lines.Take(before).Select(number).LastOrDefault(n => n is not null) ?? 0;
}
