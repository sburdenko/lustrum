using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lustrum.Core.Usage;

/// <summary>
/// Reads one line of a Claude Code session transcript (JSON Lines). A skill is used either by the model,
/// as a "Skill" tool call in an assistant entry, or by the user, as a slash command in a user entry.
/// </summary>
public static partial class TranscriptParser
{
    public const string SkillMarker = "\"Skill\"";

    // Without the angle brackets, so JSON that escapes them as \u003C still matches.
    public const string CommandMarker = "command-name";

    public static bool MightContainInvocation(string line) =>
        line.Contains(SkillMarker, StringComparison.Ordinal) || line.Contains(CommandMarker, StringComparison.Ordinal);

    public static IReadOnlyList<SkillInvocation> Parse(string line)
    {
        if (!MightContainInvocation(line))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            return FromEntry(doc.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<SkillInvocation> FromEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !TryTimestamp(entry, out var at)
            || !entry.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content))
        {
            return [];
        }

        var type = StringOf(entry, "type");
        var cwd = StringOf(entry, "cwd");
        return type switch
        {
            "assistant" => ToolCalls(content).Select(n => new SkillInvocation(n, at, cwd, InvocationKind.Model)).ToList(),
            "user" => Commands(content).Select(n => new SkillInvocation(n, at, cwd, InvocationKind.User)).ToList(),
            _ => [],
        };
    }

    private static IEnumerable<string> ToolCalls(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && StringOf(block, "type") == "tool_use"
                && StringOf(block, "name") == "Skill"
                && block.TryGetProperty("input", out var input)
                && input.ValueKind == JsonValueKind.Object
                && StringOf(input, "skill") is { Length: > 0 } skill)
            {
                yield return skill;
            }
        }
    }

    private static IEnumerable<string> Commands(JsonElement content)
    {
        var texts = content.ValueKind switch
        {
            JsonValueKind.String => [content.GetString()!],
            JsonValueKind.Array => content.EnumerateArray()
                .Where(b => b.ValueKind == JsonValueKind.Object && StringOf(b, "type") == "text")
                .Select(b => StringOf(b, "text"))
                .OfType<string>()
                .ToList(),
            _ => new List<string>(),
        };

        return texts.SelectMany(t => CommandName().Matches(t).Select(m => m.Groups[1].Value));
    }

    private static bool TryTimestamp(JsonElement entry, out DateTimeOffset at)
    {
        at = default;
        return StringOf(entry, "timestamp") is { } text
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out at);
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"<command-name>/?([^<\s]+)</command-name>")]
    private static partial Regex CommandName();
}
