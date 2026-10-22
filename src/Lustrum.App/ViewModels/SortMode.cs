namespace Lustrum.App.ViewModels;

public enum SortMode { Relevance, Name, Source, Modified, LastUsed, ContextCost }

public sealed record SortOption(SortMode Mode, string Title);
