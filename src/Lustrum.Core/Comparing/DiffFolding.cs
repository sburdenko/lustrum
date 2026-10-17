namespace Lustrum.Core.Comparing;

public static class DiffFolding
{
    public static IReadOnlyList<DiffRow> Fold(IReadOnlyList<DiffLine> lines, int context)
    {
        var rows = new List<DiffRow>();
        var index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind != DiffKind.Same)
            {
                rows.Add(lines[index++]);
                continue;
            }

            var end = index;
            while (end < lines.Count && lines[end].Kind == DiffKind.Same)
            {
                end++;
            }

            AddRun(rows, lines, index, end, context);
            index = end;
        }

        return rows;
    }

    private static void AddRun(List<DiffRow> rows, IReadOnlyList<DiffLine> lines, int start, int end, int context)
    {
        var keepHead = start == 0 ? 0 : context;
        var keepTail = end == lines.Count ? 0 : context;
        var length = end - start;
        if (length <= keepHead + keepTail + 1 || (start == 0 && end == lines.Count))
        {
            rows.AddRange(lines.Skip(start).Take(length));
            return;
        }

        rows.AddRange(lines.Skip(start).Take(keepHead));
        rows.Add(new DiffGap(length - keepHead - keepTail));
        rows.AddRange(lines.Skip(end - keepTail).Take(keepTail));
    }
}
