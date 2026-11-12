using System.Text.Json.Serialization;

namespace Kaizen.SkillEmbed.Agents.Models;

/// <summary>
/// One agent of agents.json. Its own paths are the ones on Linux; macOS and Windows have theirs only where they differ.
/// </summary>
internal sealed class AgentRow : AgentPaths
{
	public string Name { get; set; } = "";

	public string DisplayName { get; set; } = "";

	[JsonPropertyName("macos")]
	public AgentPaths? MacOS { get; set; }

	public AgentPaths? Windows { get; set; }
}
