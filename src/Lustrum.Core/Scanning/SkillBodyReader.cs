using System.Text;
using Lustrum.Core.Models;
using Lustrum.Core.Parsing;

namespace Lustrum.Core.Scanning;

public static class SkillBodyReader
{
    private const int MaxBytes = 1024 * 1024;

    public static string Read(Skill skill) => FrontmatterParser.Parse(ReadText(skill.Path)).Body;

    public static string ReadText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[(int)Math.Min(stream.Length, MaxBytes)];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
