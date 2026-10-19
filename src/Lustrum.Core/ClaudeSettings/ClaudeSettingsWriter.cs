using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.ClaudeSettings;

/// <summary>
/// Changes one <c>skillOverrides</c> entry in a Claude Code settings file and leaves every other key as it was.
/// A null value removes the entry.
/// </summary>
public static class ClaudeSettingsWriter
{
    private const string Key = "skillOverrides";

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void SetSkillVisibility(string path, string skillName, SkillVisibility? visibility)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(skillName))
        {
            throw new SettingsWriteException("There is no settings file to write to");
        }

        var target = ResolveLink(path);
        var stamp = File.Exists(target) ? File.GetLastWriteTimeUtc(target) : (DateTime?)null;
        var root = Load(target);
        if (root[Key] is not null and not JsonObject)
        {
            throw new SettingsWriteException($"{Key} in {target} is not an object, so it was left alone");
        }

        var overrides = root[Key] as JsonObject;
        if (visibility is { } value)
        {
            overrides ??= AddOverrides(root);
            overrides[skillName] = value.ToSetting();
        }
        else
        {
            overrides?.Remove(skillName);
            if (overrides is { Count: 0 })
            {
                root.Remove(Key);
            }
        }

        Save(target, root, stamp);
    }

    // A settings file linked into a dotfiles checkout must stay a link; the change goes to the real file.
    private static string ResolveLink(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).LinkTarget is not null
                ? File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path
                : path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsWriteException($"Cannot follow the link {path}: {ex.Message}", ex);
        }
    }

    private static JsonObject AddOverrides(JsonObject root)
    {
        var overrides = new JsonObject();
        root[Key] = overrides;
        return overrides;
    }

    private static JsonObject Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var text = SharedFile.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            return JsonNode.Parse(text) as JsonObject
                ?? throw new SettingsWriteException($"{path} does not hold a JSON object, so it was left alone");
        }
        catch (JsonException ex)
        {
            throw new SettingsWriteException($"{path} is not plain JSON, so it was left alone: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsWriteException($"Cannot read {path}: {ex.Message}", ex);
        }
    }

    // Written next to the target and moved over it, so Claude Code never reads a half-written file. The file
    // mode is carried over because settings can hold tokens, and a change made meanwhile is never overwritten.
    private static void Save(string path, JsonObject root, DateTime? stamp)
    {
        var temp = $"{path}.lustrum-{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(root.ToJsonString(Format) + Environment.NewLine);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (stamp is { } before)
            {
                CopyMode(path, temp);
                if (File.GetLastWriteTimeUtc(path) != before)
                {
                    throw new SettingsWriteException($"{path} changed while Lustrum was writing it; nothing was changed, try again");
                }
            }

            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsWriteException($"Cannot write {path}: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void CopyMode(string from, string to)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(to, File.GetUnixFileMode(from));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
