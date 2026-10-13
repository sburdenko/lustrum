using System.Text;
using Lustrum.Core.Models;
using Lustrum.Core.Parsing;

namespace Lustrum.Core.Scanning;

public static class SkillBodyReader
{
    private const int MaxBytes = 1024 * 1024;

    public static string Read(Skill skill)
    {
        using var stream = new FileStream(skill.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[(int)Math.Min(stream.Length, MaxBytes)];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        return FrontmatterParser.Parse(Encoding.UTF8.GetString(buffer, 0, read)).Body;
    }
}
