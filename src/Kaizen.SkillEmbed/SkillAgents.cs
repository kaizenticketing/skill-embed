using System.Text.Json;
using Kaizen.SkillEmbed.Agents;

namespace Kaizen.SkillEmbed;

/// <summary>
/// The agents the library knows. The table is generated from the agent table of
/// <see href="https://github.com/vercel-labs/skills">vercel-labs/skills</see> - docs/supported-agents.md lists it.
/// </summary>
public static class SkillAgents
{
	private const string TableResourceName = "Kaizen.SkillEmbed.agents.json";

	private static readonly Lazy<List<SkillAgent>> Table = new(Load);

	/// <summary>Every agent the library knows, in the order of the table.</summary>
	public static IReadOnlyList<SkillAgent> All => Table.Value;

	/// <summary>The agent with this name, such as <c>claude-code</c>, or null. The case of the name does not matter.</summary>
	public static SkillAgent? Find(string name)
	{
		return All.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	#region Internal Implementation

	private static List<SkillAgent> Load()
	{
		using var stream = typeof(SkillAgents).Assembly.GetManifestResourceStream(TableResourceName)
			?? throw new InvalidOperationException($"The assembly has no {TableResourceName}.");
		var table = JsonSerializer.Deserialize(stream, AgentTableJsonContext.Default.AgentTable)
			?? throw new InvalidOperationException($"{TableResourceName} is empty.");

		return table.Agents.Select(x => new SkillAgent(x)).ToList();
	}

	#endregion
}
