using System.Text;

namespace Lustrum.App.Services;

public static class CommandLine
{
    public static IReadOnlyList<string> Split(string command)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var ch in command)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                Flush(parts, current);
            }
            else
            {
                current.Append(ch);
            }
        }

        Flush(parts, current);
        return parts;
    }

    private static void Flush(List<string> parts, StringBuilder current)
    {
        if (current.Length > 0)
        {
            parts.Add(current.ToString());
            current.Clear();
        }
    }
}
