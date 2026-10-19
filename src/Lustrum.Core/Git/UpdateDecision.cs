namespace Lustrum.Core.Git;

public abstract record UpdateDecision
{
    public virtual bool CanUpdate => false;
}

public sealed record UpdateAvailable(int Behind) : UpdateDecision
{
    public override bool CanUpdate => true;
}

public sealed record UpToDate : UpdateDecision;

public sealed record UpdateBlocked(string Reason) : UpdateDecision;
