using FluentIcons.Common;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public enum Severity { Ok, Warning, Error }

public sealed record HealthPresentation(string Text, Severity Severity, Symbol Icon)
{
    public static HealthPresentation For(Health health) => health switch
    {
        Health.Ok => new("Healthy", Severity.Ok, Symbol.CheckmarkCircle),
        Health.DescriptionTooLong => new("Description is over 1024 characters", Severity.Warning, Symbol.Warning),
        Health.MissingName => new("Missing name", Severity.Error, Symbol.ErrorCircle),
        Health.MissingDescription => new("Missing description", Severity.Error, Symbol.ErrorCircle),
        Health.NoFrontmatter => new("No frontmatter", Severity.Error, Symbol.ErrorCircle),
        Health.ParseError => new("Invalid YAML", Severity.Error, Symbol.ErrorCircle),
        Health.Orphaned => new("Source not found", Severity.Warning, Symbol.Warning),
        _ => new(health.ToString(), Severity.Warning, Symbol.Warning),
    };

    public bool IsOk => Severity == Severity.Ok;

    public bool IsWarning => Severity == Severity.Warning;

    public bool IsError => Severity == Severity.Error;
}
