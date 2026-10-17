using System.Text;

namespace Lustrum.Core.Scanning;

/// <summary>
/// Opens files for reading without stopping anyone else from writing, renaming or deleting them. On Windows the
/// default share mode would make Claude Code fail to save ~/.claude.json while Lustrum happened to read it.
/// </summary>
public static class SharedFile
{
    private const FileShare EveryoneElse = FileShare.ReadWrite | FileShare.Delete;

    public static FileStream OpenRead(string path, int bufferSize = 4096) =>
        new(path, FileMode.Open, FileAccess.Read, EveryoneElse, bufferSize);

    public static string ReadAllText(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
