using Lustrum.Core.Models;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public abstract record UsageState
{
    public static UsageState Loading { get; } = new LoadingUsage();

    public virtual UsageReport? Report => null;

    public virtual bool CanJudgeUnused => false;

    /// <summary>The recorded uses of this copy; for a name several copies share, an estimate.</summary>
    public virtual SkillUsage? UsageFor(Skill skill) => null;

    /// <summary>Whether no recorded use ran this copy; meaningful only when <see cref="CanJudgeUnused"/>.</summary>
    public virtual bool IsUnused(Skill skill) => UsageFor(skill) is null;

    public abstract string Status { get; }

    public abstract string Describe(Skill skill);
}

public sealed record LoadingUsage : UsageState
{
    public override string Status => "Reading Claude Code transcripts…";

    public override string Describe(Skill skill) => "Reading Claude Code transcripts…";
}

public sealed record FailedUsage(string Reason) : UsageState
{
    public override string Status => "Usage unknown";

    public override string Describe(Skill skill) => "Usage unknown: Claude Code transcripts could not be read";
}

/// <summary>Usage read from the transcripts; Copies splits shared names among their copies once a scan is known.</summary>
public sealed record ReadyUsage(UsageReport Value, DateTimeOffset At, CopyUsage? Copies = null) : UsageState
{
    public override UsageReport? Report => Value;

    public override SkillUsage? UsageFor(Skill skill) => Copies is { } copies ? copies.For(skill) : Value.For(skill.FullName);

    public override bool IsUnused(Skill skill) => Copies is { } copies ? copies.IsUnused(skill) : base.IsUnused(skill);

    public override bool CanJudgeUnused => Value.TranscriptCount > 0;

    public override string Status
    {
        get
        {
            if (Value.TranscriptCount == 0)
            {
                return "No Claude Code transcripts found";
            }

            var noun = Value.TranscriptCount == 1 ? "transcript" : "transcripts";
            var unreadable = Value.Issues.Count == 0 ? string.Empty : $", {Value.Issues.Count} unreadable";
            return $"Usage from {Value.TranscriptCount} {noun}, as of {At.ToLocalTime():HH:mm}{unreadable}";
        }
    }

    public override string Describe(Skill skill)
    {
        if (Value.TranscriptCount == 0)
        {
            return "Usage unknown: no Claude Code transcripts found";
        }

        if (Copies is { } copies && copies.IsEstimate(skill))
        {
            return Shared(skill, copies);
        }

        if (UsageFor(skill) is not { } usage)
        {
            return "No recorded use in Claude Code transcripts";
        }

        var times = usage.Count == 1 ? "time" : "times";
        return $"Used {usage.Count} {times} ({usage.ByModel} by Claude, {usage.ByUser} by you), last on {usage.LastUsed.ToLocalTime():d MMM yyyy}";
    }

    private static string Shared(Skill skill, CopyUsage copies)
    {
        if (copies.ForName(skill) is not { } all)
        {
            return $"No recorded use of {skill.InvocationName} in Claude Code transcripts";
        }

        var uses = all.Count == 1 ? "use" : "uses";
        var share = copies.For(skill) is { } mine
            ? $"About {mine.Count} of the {all.Count} {uses} of {skill.InvocationName} ran this copy, last on {mine.LastUsed.ToLocalTime():d MMM yyyy}"
            : $"None of the {all.Count} {uses} of {skill.InvocationName} ran this copy";
        var open = copies.Unattributed(skill) is var count and > 0 ? $"; {count} could not be tied to one copy" : string.Empty;
        return $"{share}{open}; an estimate from the folders the sessions ran in";
    }
}
