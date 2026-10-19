namespace Lustrum.Core.ClaudeSettings;

public sealed class SettingsWriteException(string message, Exception? inner = null) : Exception(message, inner);
