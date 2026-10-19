namespace Lustrum.Core.Git;

/// <summary>
/// SkillsOnly repositories sit inside a skills or commands folder, so updating them touches nothing but skills.
/// Containing repositories hold a skill as part of something larger, usually a project or a dotfiles checkout.
/// </summary>
public enum RepositoryRole { SkillsOnly, Containing }
