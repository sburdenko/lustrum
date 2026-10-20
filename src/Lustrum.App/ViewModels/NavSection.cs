using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

/// <summary>
/// A top-level row of the source column, with its shortcut key and its children flattened. Selection is updated in
/// place, so clicking a row never rebuilds the column under the pointer.
/// </summary>
public sealed partial class NavSection : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlert))]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isOpen;

    private NavSection(NavNode node, IReadOnlyList<NavChild> children)
    {
        Node = node;
        Key = KeyFor(node.Scope);
        Children = children;
    }

    public NavNode Node { get; }

    public string Key { get; }

    public IReadOnlyList<NavChild> Children { get; }

    public bool IsProblems => Node.Scope is ProblemsScope;

    public bool IsAlert => IsProblems && Node.Count > 0 && !IsSelected;

    public string Title => Node.Title.ToUpperInvariant();

    public static string KeyFor(SkillScope scope) => scope switch
    {
        AllScope => "A",
        KindScope { Kind: SourceKind.Managed } => "M",
        KindScope { Kind: SourceKind.Global } => "G",
        KindScope { Kind: SourceKind.Project } => "P",
        KindScope { Kind: SourceKind.Plugin } => "L",
        KindScope { Kind: SourceKind.Synced } => "S",
        ProblemsScope => "!",
        _ => string.Empty,
    };

    public static IReadOnlyList<NavSection> Build(IReadOnlyList<NavNode> roots, NavNode? selected)
    {
        var sections = roots.Select(root => new NavSection(root, Flatten(root.Children, 1).ToList())).ToList();
        Select(sections, selected);
        return sections;
    }

    public static void Select(IEnumerable<NavSection> sections, NavNode? selected)
    {
        foreach (var section in sections)
        {
            section.IsSelected = section.Node.Scope == selected?.Scope;
            foreach (var child in section.Children)
            {
                child.IsSelected = child.Node.Scope == selected?.Scope;
            }

            section.IsOpen = section.IsSelected || section.Children.Any(c => c.IsSelected);
        }
    }

    private static IEnumerable<NavChild> Flatten(IEnumerable<NavNode> nodes, int depth) =>
        nodes.SelectMany(n => new[] { new NavChild(n, depth) }.Concat(Flatten(n.Children, depth + 1)));
}

public sealed partial class NavChild(NavNode node, int depth) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public NavNode Node { get; } = node;

    public int Depth { get; } = depth;

    public double Indent => 20 + Depth * 14;
}
