using System.Collections.Concurrent;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Indexing;

public static class SkillBodySearch
{
    public static async Task<IReadOnlySet<string>> FindAsync(
        IReadOnlyList<Skill> skills,
        string text,
        CancellationToken cancellationToken)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var found = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        if (words.Length == 0)
        {
            return new HashSet<string>();
        }

        var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount };
        await Parallel.ForEachAsync(skills, options, (skill, _) =>
        {
            if (BodyContainsAll(skill, words))
            {
                found.TryAdd(skill.Id, 0);
            }

            return ValueTask.CompletedTask;
        });

        return found.Keys.ToHashSet(StringComparer.Ordinal);
    }

    private static bool BodyContainsAll(Skill skill, string[] words)
    {
        try
        {
            var body = SkillBodyReader.Read(skill);
            return words.All(w => body.Contains(w, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
