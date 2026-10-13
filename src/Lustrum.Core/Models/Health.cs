namespace Lustrum.Core.Models;

public enum Health
{
    Ok,
    MissingName,
    MissingDescription,
    NoFrontmatter,
    ParseError,
    DescriptionTooLong,
    Orphaned,
}
