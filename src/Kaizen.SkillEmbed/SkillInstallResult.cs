namespace Kaizen.SkillEmbed;

/// <summary>
/// What an install, a refresh or a remove did to one directory.
/// </summary>
/// <param name="Directory">The skill's own directory, as a full path.</param>
/// <param name="Agents">The agents that read the skill from there. A refresh is given directories, so it names none.</param>
/// <param name="Outcome">What happened.</param>
/// <param name="Error">The file system's message, when the outcome is <see cref="SkillInstallOutcome.Failed"/>.</param>
public sealed record SkillInstallResult(
	string Directory,
	IReadOnlyList<SkillAgent> Agents,
	SkillInstallOutcome Outcome,
	string? Error = null
);
