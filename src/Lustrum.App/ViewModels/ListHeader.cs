namespace Lustrum.App.ViewModels;

public interface IListRow
{
    bool IsSelectable { get; }
}

/// <summary>A group heading in the skill list, such as GLOBAL with its count.</summary>
public sealed record ListHeader(string Title, int Count) : IListRow
{
    public bool IsSelectable => false;
}
