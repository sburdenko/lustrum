using Lustrum.Core.Git;

namespace Lustrum.App.Services;

public sealed record AppServices(
    IShellService Shell,
    IClipboardService Clipboard,
    ISettingsService Settings,
    IThemeService Theme,
    ISkillGit Git,
    ISkillWriter Writer);
