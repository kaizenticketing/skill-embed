namespace KaizenTicketing.SkillEmbed.Agents.Models;

/// <summary>
/// Where an agent keeps its skills and what shows that it is installed, as path templates.
/// </summary>
internal class AgentPaths
{
	/// <summary>The skills directory's alternatives: the first one whose environment variables are set is it.</summary>
	public List<string> SkillsDirectory { get; set; } = [];

	/// <summary>
	/// The agent is installed when any of these paths exists. Each path has alternatives, as the skills directory has.
	/// </summary>
	public List<List<string>> Detect { get; set; } = [];
}
