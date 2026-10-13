namespace Lustrum.App.ViewModels;

public enum SortMode { Name, Source, Modified }

public sealed record SortOption(SortMode Mode, string Title);
