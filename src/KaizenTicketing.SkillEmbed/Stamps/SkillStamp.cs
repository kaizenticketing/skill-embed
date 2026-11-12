namespace KaizenTicketing.SkillEmbed.Stamps;

/// <summary>
/// What the installer leaves in a skill's directory, as <c>.skill-embed.json</c>: who wrote the skill there and what
/// it wrote. A directory without one was not written by an installer, and is never touched.
/// </summary>
internal sealed class SkillStamp
{
	/// <summary>The program that installed the skill.</summary>
	public string Owner { get; set; } = "";

	/// <summary>That program's version at the time.</summary>
	public string Version { get; set; } = "";

	/// <summary>The <see cref="EmbeddedSkill.ContentHash"/> of the skill it wrote.</summary>
	public string ContentHash { get; set; } = "";

	/// <summary>Each file it wrote, by its path, with the hash of its content.</summary>
	public Dictionary<string, string> Files { get; set; } = [];
}
