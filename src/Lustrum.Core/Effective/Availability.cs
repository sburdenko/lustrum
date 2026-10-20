namespace Lustrum.Core.Effective;

/// <summary>
/// Active skills load when a session starts, shadowed ones lose their name to another copy, and on-demand ones sit in a
/// folder below the project and load once Claude works on files there.
/// </summary>
public enum Availability { Active, Shadowed, OnDemand }
