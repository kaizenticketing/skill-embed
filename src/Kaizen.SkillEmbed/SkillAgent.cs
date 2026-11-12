using Kaizen.SkillEmbed.Agents;
using Kaizen.SkillEmbed.Agents.Models;

namespace Kaizen.SkillEmbed;

/// <summary>
/// An AI coding agent that reads skills from a directory of the user's: where that directory is, and what shows that
/// the agent is installed. <see cref="SkillAgents.All"/> has the ones the library knows; make your own for any other.
/// </summary>
/// <remarks>
/// A path is a template: forward slashes, <c>{home}</c> for the user's home directory and <c>{env:NAME}</c> for an
/// environment variable, such as <c>{home}/.claude/skills</c>.
/// </remarks>
public sealed class SkillAgent
{
	private readonly AgentPaths paths;
	private readonly AgentPaths? macOS;
	private readonly AgentPaths? windows;

	/// <summary>Makes an agent the library does not know.</summary>
	/// <param name="name">Its name in commands and flags, such as <c>claude-code</c>.</param>
	/// <param name="displayName">Its name for people, such as <c>Claude Code</c>.</param>
	/// <param name="skillsDirectory">The directory that holds one directory for each skill.</param>
	/// <param name="detect">The agent is installed when any of these paths exists.</param>
	public SkillAgent(string name, string displayName, string skillsDirectory, params string[] detect)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
		ArgumentException.ThrowIfNullOrWhiteSpace(skillsDirectory);
		ArgumentNullException.ThrowIfNull(detect);
		if (detect.Length == 0)
			throw new ArgumentException("An agent needs a path that shows it is installed.", nameof(detect));

		Name = name;
		DisplayName = displayName;
		paths = new AgentPaths
		{
			SkillsDirectory = [skillsDirectory],
			Detect = detect.Select(x => new List<string> { x }).ToList(),
		};
	}

	internal SkillAgent(AgentRow row)
	{
		Name = row.Name;
		DisplayName = row.DisplayName;
		paths = row;
		macOS = row.MacOS;
		windows = row.Windows;
	}

	/// <summary>The agent's name in commands and flags, such as <c>claude-code</c>.</summary>
	public string Name { get; }

	/// <summary>The agent's name for people, such as <c>Claude Code</c>.</summary>
	public string DisplayName { get; }

	/// <summary>
	/// The directory that holds one directory for each of the user's skills, as a full path. Null when it hangs on an
	/// environment variable that is not set.
	/// </summary>
	public string? GetSkillsDirectory(SkillEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		return PathTemplate.ResolveFirst(PathsOn(environment.Platform).SkillsDirectory, environment);
	}

	/// <summary>True when the agent's own directory is on the machine.</summary>
	public bool IsInstalled(SkillEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		foreach (var alternatives in PathsOn(environment.Platform).Detect)
		{
			var path = PathTemplate.ResolveFirst(alternatives, environment);
			if (path != null && (Directory.Exists(path) || File.Exists(path)))
				return true;
		}

		return false;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return Name;
	}

	#region Internal Implementation

	private AgentPaths PathsOn(SkillPlatform platform)
	{
		if (platform == SkillPlatform.MacOS && macOS != null)
			return macOS;
		if (platform == SkillPlatform.Windows && windows != null)
			return windows;

		return paths;
	}

	#endregion
}
