using FluentIcons.Common;
using Lustrum.Core.Indexing;

namespace Lustrum.App.ViewModels;

public sealed record NavNode(
    string Title,
    SkillScope Scope,
    int Count,
    Symbol Icon,
    IReadOnlyList<NavNode> Children,
    bool IsDimmed = false,
    string? Hint = null)
{
    public bool HasCount => Count > 0;

    public double TitleOpacity => IsDimmed ? 0.5 : 1.0;
}
