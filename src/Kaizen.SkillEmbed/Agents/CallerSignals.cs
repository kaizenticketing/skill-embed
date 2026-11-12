namespace Kaizen.SkillEmbed.Agents;

/// <summary>
/// The environment variables each agent sets in the shell it runs commands in, ported from
/// <see href="https://www.npmjs.com/package/@vercel/detect-agent">@vercel/detect-agent</see> 1.2.7 (Apache-2.0, see
/// THIRD-PARTY-NOTICES.txt) with the refinement vercel-labs/skills makes to it. The first signal that matches wins.
/// </summary>
internal static class CallerSignals
{
	private const string AiAgentVariable = "AI_AGENT";

	// the names AI_AGENT may carry that are not a name of the agent table
	private static readonly Dictionary<string, string> AiAgentAliases = new(StringComparer.OrdinalIgnoreCase)
	{
		["claude"] = "claude-code",
		["cowork"] = "claude-code",
		["gemini"] = "gemini-cli",
		["cursor-cli"] = "cursor",
		["augment-cli"] = "augment",
		["github-copilot-cli"] = "github-copilot",
	};

	// CURSOR_TRACE_ID is left out: Cursor sets it in its own terminal too, where a person types
	private static readonly (string Agent, Func<SkillEnvironment, bool> IsSet)[] Signals =
	[
		("cursor", x => x.GetVariable("CURSOR_AGENT") != null || x.GetVariable("CURSOR_EXTENSION_HOST_ROLE") == "agent-exec"),
		("gemini-cli", x => x.GetVariable("GEMINI_CLI") != null),
		("codex", x => x.GetVariable("CODEX_SANDBOX") != null || x.GetVariable("CODEX_CI") != null || x.GetVariable("CODEX_THREAD_ID") != null),
		("antigravity", x => x.GetVariable("ANTIGRAVITY_AGENT") != null),
		("augment", x => x.GetVariable("AUGMENT_AGENT") != null),
		("opencode", x => x.GetVariable("OPENCODE") == "1" || x.GetVariable("OPENCODE_CLIENT") != null),
		("claude-code", x => x.GetVariable("CLAUDECODE") != null || x.GetVariable("CLAUDE_CODE") != null),
		("github-copilot", x => x.GetVariable("COPILOT_MODEL") != null || x.GetVariable("COPILOT_ALLOW_ALL") != null || x.GetVariable("COPILOT_GITHUB_TOKEN") != null),
	];

	/// <summary>
	/// The name, in the agent table, of the agent that runs this process, or null. An agent that announces itself
	/// with AI_AGENT, and that the table does not have, is null too: there is nowhere to look for its skills.
	/// </summary>
	public static string? Detect(SkillEnvironment environment)
	{
		var announced = environment.GetVariable(AiAgentVariable);
		if (announced != null)
			return AiAgentAliases.GetValueOrDefault(announced, announced);

		foreach (var (agent, isSet) in Signals)
		{
			if (isSet(environment))
				return agent;
		}

		return null;
	}
}
