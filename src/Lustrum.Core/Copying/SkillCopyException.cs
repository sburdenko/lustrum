namespace Lustrum.Core.Copying;

public sealed class SkillCopyException(string message, Exception? inner = null) : Exception(message, inner);
