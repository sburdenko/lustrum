using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Lustrum.Core.Comparing;

public static class SkillComparer
{
    public static SkillDiff Compare(string left, string right)
    {
        var model = InlineDiffBuilder.Diff(Normalize(left), Normalize(right), ignoreWhiteSpace: false, ignoreCase: false);
        var lines = new List<DiffLine>(model.Lines.Count);
        var leftNumber = 0;
        var rightNumber = 0;
        foreach (var piece in model.Lines)
        {
            switch (piece.Type)
            {
                case ChangeType.Deleted:
                    lines.Add(new DiffLine(DiffKind.Removed, piece.Text, ++leftNumber, null));
                    break;
                case ChangeType.Inserted:
                    lines.Add(new DiffLine(DiffKind.Added, piece.Text, null, ++rightNumber));
                    break;
                case ChangeType.Unchanged:
                    lines.Add(new DiffLine(DiffKind.Same, piece.Text, ++leftNumber, ++rightNumber));
                    break;
            }
        }

        return new SkillDiff(lines);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');
}
