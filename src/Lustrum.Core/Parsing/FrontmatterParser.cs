using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Lustrum.Core.Parsing;

public static partial class FrontmatterParser
{
    private const string Fence = "---";
    private const string AllowedToolsKey = "allowed-tools";
    private const string InvalidSyntax = "syntax error, check brackets, quotes and colons";

    private static readonly IDeserializer Yaml = new DeserializerBuilder().Build();

    public static FrontmatterResult Parse(string text)
    {
        var lines = Normalize(text).Split('\n');
        if (lines[0].TrimEnd() != Fence)
        {
            return FrontmatterResult.Missing(string.Join('\n', lines));
        }

        var closing = FindClosingFence(lines);
        if (closing < 0)
        {
            return FrontmatterResult.Invalid(string.Join('\n', lines), "Frontmatter is not closed with ---");
        }

        var body = string.Join('\n', lines.Skip(closing + 1));
        var yaml = string.Join('\n', lines.Skip(1).Take(closing - 1).Select(ReplaceLeadingTabs));
        return ParseYaml(yaml, body);
    }

    private static FrontmatterResult ParseYaml(string yaml, string body)
    {
        Dictionary<string, object?>? map;
        try
        {
            map = Yaml.Deserialize<Dictionary<string, object?>?>(yaml);
        }
        catch (Exception ex) when (ex is YamlException or InvalidOperationException)
        {
            return FrontmatterResult.Invalid(body, Describe(SyntaxError(yaml) ?? ex as YamlException));
        }

        if (map is null)
        {
            return new FrontmatterResult(FrontmatterState.Parsed, new Dictionary<string, string>(), [], body, null);
        }

        var fields = map.ToDictionary(pair => pair.Key, pair => Stringify(pair.Value));
        var allowedTools = map.TryGetValue(AllowedToolsKey, out var raw) ? ToList(raw) : [];
        return new FrontmatterResult(FrontmatterState.Parsed, fields, allowedTools, body, null);
    }

    private static YamlException? SyntaxError(string yaml)
    {
        try
        {
            new YamlStream().Load(new StringReader(yaml));
            return null;
        }
        catch (YamlException ex)
        {
            return ex;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string Describe(YamlException? ex)
    {
        if (ex is null)
        {
            return InvalidSyntax;
        }

        var cause = ex;
        while (cause.InnerException is YamlException inner)
        {
            cause = inner;
        }

        var reason = cause.InnerException is null ? cause.Message : InvalidSyntax;
        return $"Line {cause.Start.Line + 1}: {reason}";
    }

    private static int FindClosingFence(string[] lines)
    {
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == Fence)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Normalize(string text) =>
        text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');

    private static string ReplaceLeadingTabs(string line) =>
        LeadingTabs().Replace(line, m => new string(' ', m.Length * 2));

    private static string Stringify(object? value) => value switch
    {
        null => string.Empty,
        string s => s.Trim(),
        IDictionary<object, object> map => string.Join("; ", map.Select(p => $"{p.Key}: {Stringify(p.Value)}")),
        IEnumerable<object> list => string.Join(", ", list.Select(Stringify)),
        _ => value.ToString() ?? string.Empty,
    };

    private static IReadOnlyList<string> ToList(object? value) => value switch
    {
        null => [],
        string s => SplitTopLevel(s),
        IEnumerable<object> list => list.Select(Stringify).Where(s => s.Length > 0).ToList(),
        _ => [Stringify(value)],
    };

    private static List<string> SplitTopLevel(string text)
    {
        var items = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        foreach (var ch in text)
        {
            depth += ch == '(' ? 1 : ch == ')' ? -1 : 0;
            if (ch == ',' && depth == 0)
            {
                Flush(items, current);
                continue;
            }

            current.Append(ch);
        }

        Flush(items, current);
        return items;
    }

    private static void Flush(List<string> items, StringBuilder current)
    {
        var item = current.ToString().Trim();
        if (item.Length > 0)
        {
            items.Add(item);
        }

        current.Clear();
    }

    [GeneratedRegex(@"^\t+")]
    private static partial Regex LeadingTabs();
}
