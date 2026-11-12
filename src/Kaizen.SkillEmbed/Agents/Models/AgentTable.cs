namespace Kaizen.SkillEmbed.Agents.Models;

/// <summary>
/// agents.json, which scripts/generate-agents.mjs writes.
/// </summary>
internal sealed class AgentTable
{
	public List<AgentRow> Agents { get; set; } = [];
}
