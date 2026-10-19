using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Copying;

namespace Lustrum.App.Services;

public sealed class SkillWriter : ISkillWriter
{
    private readonly SemaphoreSlim _settings = new(1, 1);

    public async Task SetVisibilityAsync(string settingsPath, string skillName, SkillVisibility? visibility)
    {
        await _settings.WaitAsync();
        try
        {
            await Task.Run(() => ClaudeSettingsWriter.SetSkillVisibility(settingsPath, skillName, visibility));
        }
        finally
        {
            _settings.Release();
        }
    }

    public Task<CopyResult> CopyAsync(ReadyCopy plan) => Task.Run(() => SkillCopier.Execute(plan));
}
