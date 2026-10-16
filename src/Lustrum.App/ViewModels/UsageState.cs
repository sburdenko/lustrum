using Lustrum.Core.Models;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public abstract record UsageState
{
    public static UsageState Loading { get; } = new LoadingUsage();

    public virtual UsageReport? Report => null;

    public virtual bool CanJudgeUnused => false;

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

public sealed record ReadyUsage(UsageReport Value, DateTimeOffset At) : UsageState
{
    public override UsageReport? Report => Value;

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

        if (Value.For(skill.FullName) is not { } usage)
        {
            return "No recorded use in Claude Code transcripts";
        }

        var times = usage.Count == 1 ? "time" : "times";
        return $"Used {usage.Count} {times} ({usage.ByModel} by Claude, {usage.ByUser} by you), last on {usage.LastUsed.ToLocalTime():d MMM yyyy}";
    }
}
