namespace KaizenTicketing.SkillEmbed;

/// <summary>
/// What a skill's directory in an agent holds, against the skill the program carries.
/// </summary>
public enum SkillInstallationState
{
	/// <summary>The agent has no directory for the skill.</summary>
	NotInstalled,

	/// <summary>The installer wrote the skill there, and it is the one the program carries.</summary>
	Current,

	/// <summary>The installer wrote the skill there, from an older or a newer version of the program.</summary>
	Outdated,

	/// <summary>The installer wrote the skill there, and a file has been changed since.</summary>
	Modified,

	/// <summary>
	/// Something with the skill's name is there that this program's installer did not write, or that it cannot read.
	/// The installer never touches it.
	/// </summary>
	Foreign,
}
