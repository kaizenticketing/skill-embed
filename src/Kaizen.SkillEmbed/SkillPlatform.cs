namespace Kaizen.SkillEmbed;

/// <summary>
/// The operating system a <see cref="SkillEnvironment"/> is on. A few agents keep their files in a different place on
/// each.
/// </summary>
public enum SkillPlatform
{
	/// <summary>Linux, and anything else that is neither macOS nor Windows.</summary>
	Linux,

	/// <summary>macOS.</summary>
	MacOS,

	/// <summary>Windows.</summary>
	Windows,
}
