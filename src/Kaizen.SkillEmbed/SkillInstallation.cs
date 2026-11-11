namespace Kaizen.SkillEmbed;

/// <summary>
/// The skill in one place on the machine.
/// </summary>
/// <param name="Directory">The skill's own directory, as a full path - the agent's skills directory and the skill's name.</param>
/// <param name="Agents">The agents that read the skill from there. Several agents share <c>~/.agents/skills</c>.</param>
/// <param name="State">What the directory holds.</param>
/// <param name="InstalledVersion">The version of the program that wrote it, when an installer did.</param>
public sealed record SkillInstallation(
	string Directory,
	IReadOnlyList<SkillAgent> Agents,
	SkillInstallationState State,
	string? InstalledVersion
);
