namespace Lustrum.Core.ClaudeSettings;

/// <summary>Managed settings the operating system holds: configuration profiles and registry policies.</summary>
public interface IPolicyStore
{
    /// <summary>Device policies (macOS profile, Windows HKLM), lowest priority first; later ones win key by key.</summary>
    IReadOnlyList<PolicyText> DevicePolicies();

    /// <summary>The Windows HKCU policy value, or null when there is none.</summary>
    PolicyText? UserPolicy();
}

public sealed class NoPolicyStore : IPolicyStore
{
    public static NoPolicyStore Instance { get; } = new();

    public IReadOnlyList<PolicyText> DevicePolicies() => [];

    public PolicyText? UserPolicy() => null;
}
