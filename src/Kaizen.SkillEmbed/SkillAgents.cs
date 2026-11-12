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

	/// <summary>
	/// The agent that runs this program, by the environment variables it sets in the shell it runs commands in - or
	/// null when a person, a script or an agent the library does not know runs it.
	/// </summary>
	/// <remarks>
	/// Use it to tell an agent about your skill when it does not have it: an agent reads what your command writes,
	/// a person at a terminal never sees it. Claude Code, Codex, Cursor, Gemini CLI, Antigravity, Augment, OpenCode
	/// and GitHub Copilot are detected, and any agent that sets <c>AI_AGENT</c> to its name.
	/// </remarks>
	/// <param name="environment">The process to look at. Default: <see cref="SkillEnvironment.Current"/>.</param>
	public static SkillAgent? DetectCaller(SkillEnvironment? environment = null)
	{
		var name = CallerSignals.Detect(environment ?? SkillEnvironment.Current);
		return name == null ? null : Find(name);
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
