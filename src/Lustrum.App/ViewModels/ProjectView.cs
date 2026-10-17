namespace Lustrum.App.ViewModels;

public enum ProjectView { Own, Effective, Missing }

public sealed record ProjectViewOption(ProjectView View, string Title, string Hint);
