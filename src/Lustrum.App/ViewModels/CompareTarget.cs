using Lustrum.Core.Comparing;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.App.ViewModels;

/// <summary>Something the COMPARE tab can set this skill against.</summary>
public abstract record CompareTarget(string Label)
{
    public abstract (string Left, string Right) Read(Skill skill);

    public abstract string Legend { get; }

    public abstract string Summarize(SkillDiff diff);

    protected static string Lines(int count) => count == 1 ? "1 line" : $"{count} lines";
}

/// <summary>Another copy of the same name.</summary>
public sealed record SkillCompareTarget(string Label, Skill Skill) : CompareTarget(Label)
{
    public override (string Left, string Right) Read(Skill skill) => (SkillBodyReader.ReadText(skill.Path), SkillBodyReader.ReadText(Skill.Path));

    public override string Legend => $"− only in this copy   + only in {Label}";

    public override string Summarize(SkillDiff diff) => diff.IsIdentical
        ? $"Identical to the copy in {Label}"
        : $"{Lines(diff.Removed)} only in this copy, {Lines(diff.Added)} only in {Label}";
}

/// <summary>The skill's main file as committed against the same file in the fetched upstream.</summary>
public sealed record IncomingCompareTarget(string Label, SkillIncoming Incoming) : CompareTarget(Label)
{
    public override (string Left, string Right) Read(Skill skill) => (Incoming.Before ?? string.Empty, Incoming.After ?? string.Empty);

    public override string Legend => "− goes with the update   + comes with the update";

    public override string Summarize(SkillDiff diff) => diff.IsIdentical
        ? $"The update leaves {Path.GetFileName(Incoming.MainFile)} as it is"
        : $"The update removes {Lines(diff.Removed)} and adds {Lines(diff.Added)} in {Path.GetFileName(Incoming.MainFile)}";
}
