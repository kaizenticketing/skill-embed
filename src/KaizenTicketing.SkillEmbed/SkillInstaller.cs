using System.Security;
using System.Text.Json;
using KaizenTicketing.SkillEmbed.Stamps;

namespace KaizenTicketing.SkillEmbed;

/// <summary>
/// Puts a program's <see cref="EmbeddedSkill"/> into the AI coding agents on the machine, and keeps those copies the
/// ones the program carries.
/// </summary>
/// <remarks>
/// <para>
/// The installer copies the skill's files into each agent's skills directory, under the skill's name, and leaves a
/// stamp there, <c>.skill-embed.json</c>. The stamp is how it knows a directory as its own: it never touches a
/// directory without its owner's stamp, and it leaves a file the user has changed alone unless it is forced.
/// </para>
/// <para>
/// A file system that refuses - a sandbox, a read-only home - is an outcome, not an exception, so a caller can
/// refresh at the start of every command without that ever stopping the command.
/// </para>
/// </remarks>
public sealed class SkillInstaller
{
	private const string TemporarySuffix = ".skill-embed-tmp";

	private readonly string owner;
	private readonly string version;
	private readonly SkillEnvironment environment;
	private readonly IReadOnlyList<SkillAgent> agents;

	/// <summary>Makes an installer for one skill.</summary>
	/// <param name="skill">The skill the program carries.</param>
	/// <param name="owner">
	/// The program's name, such as <c>kz</c>. It goes in the stamp, and the installer only ever touches a directory
	/// whose stamp has it - so never change it once the program has shipped.
	/// </param>
	/// <param name="version">The program's version, which the stamp keeps for people to read.</param>
	/// <param name="environment">The machine to work on. Default: <see cref="SkillEnvironment.Current"/>.</param>
	/// <param name="agents">The agents to look for. Default: <see cref="SkillAgents.All"/>.</param>
	public SkillInstaller(
		EmbeddedSkill skill,
		string owner,
		string version,
		SkillEnvironment? environment = null,
		IEnumerable<SkillAgent>? agents = null)
	{
		ArgumentNullException.ThrowIfNull(skill);
		ArgumentException.ThrowIfNullOrWhiteSpace(owner);
		ArgumentException.ThrowIfNullOrWhiteSpace(version);

		Skill = skill;
		this.owner = owner;
		this.version = version;
		this.environment = environment ?? SkillEnvironment.Current;
		this.agents = agents == null ? SkillAgents.All : agents.ToList();
	}

	/// <summary>The skill the program carries.</summary>
	public EmbeddedSkill Skill { get; }

	/// <summary>The agents that are installed on the machine.</summary>
	public IReadOnlyList<SkillAgent> DetectAgents()
	{
		return agents.Where(x => x.IsInstalled(environment)).ToList();
	}

	/// <summary>
	/// Where the skill is, or would go, and what is there now. Nothing is written.
	/// </summary>
	/// <param name="agents">The agents to look at. Default: the ones <see cref="DetectAgents"/> finds.</param>
	public IReadOnlyList<SkillInstallation> GetInstallations(IEnumerable<SkillAgent>? agents = null)
	{
		var installations = new List<SkillInstallation>();
		foreach (var target in Targets(agents))
		{
			var inspection = Inspect(target.Directory);
			installations.Add(new SkillInstallation(target.Directory, target.Agents, inspection.State, inspection.Stamp?.Version));
		}

		return installations;
	}

	/// <summary>
	/// Installs the skill into each agent, or makes the copy that is there the one the program carries.
	/// </summary>
	/// <param name="agents">The agents to install into. Default: the ones <see cref="DetectAgents"/> finds.</param>
	/// <param name="force">Also overwrite a copy whose files have been changed since the installer wrote them.</param>
	/// <returns>One result for each directory - agents that share a skills directory share a result.</returns>
	public IReadOnlyList<SkillInstallResult> Install(IEnumerable<SkillAgent>? agents = null, bool force = false)
	{
		return Targets(agents)
			.Select(x => Write(x.Directory, x.Agents, force, installWhenMissing: true))
			.ToList();
	}

	/// <summary>
	/// Makes the copies in these directories the one the program carries. It installs nothing new: a directory the
	/// skill is no longer in comes back as <see cref="SkillInstallOutcome.NotInstalled"/>.
	/// </summary>
	/// <remarks>
	/// Keep each <see cref="SkillInstallResult.Directory"/> an install gave, with <see cref="EmbeddedSkill.ContentHash"/>.
	/// Then at the start of a command, when the hash you kept is not the skill's, refresh those directories.
	/// </remarks>
	/// <param name="directories">The skill's own directories, as an install's results gave them.</param>
	public IReadOnlyList<SkillInstallResult> Refresh(IEnumerable<string> directories)
	{
		ArgumentNullException.ThrowIfNull(directories);

		return directories
			.Select(x => Write(Path.GetFullPath(x), [], force: false, installWhenMissing: false))
			.ToList();
	}

	/// <summary>
	/// Removes the skill from each agent: the files the installer wrote, and the directory when nothing else is in it.
	/// </summary>
	/// <param name="agents">The agents to remove it from. Default: the ones <see cref="DetectAgents"/> finds.</param>
	/// <param name="force">Also remove a copy whose files have been changed since the installer wrote them.</param>
	public IReadOnlyList<SkillInstallResult> Remove(IEnumerable<SkillAgent>? agents = null, bool force = false)
	{
		return Targets(agents)
			.Select(x => Delete(x.Directory, x.Agents, force))
			.ToList();
	}

	#region Internal Implementation

	private sealed record Target(string Directory, List<SkillAgent> Agents);

	private sealed record Inspection(SkillInstallationState State, SkillStamp? Stamp);

	// the skill's directory for each agent - agents that share a skills directory share a target
	private List<Target> Targets(IEnumerable<SkillAgent>? chosen)
	{
		var comparison = environment.Platform == SkillPlatform.Linux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		var targets = new List<Target>();
		foreach (var agent in chosen ?? DetectAgents())
		{
			var skillsDirectory = agent.GetSkillsDirectory(environment);
			if (skillsDirectory == null)
				continue;

			var directory = Path.Combine(skillsDirectory, Skill.Name);
			var target = targets.FirstOrDefault(x => string.Equals(x.Directory, directory, comparison));
			if (target == null)
			{
				target = new Target(directory, []);
				targets.Add(target);
			}
			target.Agents.Add(agent);
		}

		return targets;
	}

	private Inspection Inspect(string directory)
	{
		try
		{
			if (!Directory.Exists(directory))
				return new Inspection(File.Exists(directory) ? SkillInstallationState.Foreign : SkillInstallationState.NotInstalled, null);

			var stamp = ReadStamp(directory);
			if (stamp == null)
				return new Inspection(SkillInstallationState.Foreign, null);

			var carried = Skill.Files.ToDictionary(x => x.RelativePath, x => x.Hash, StringComparer.Ordinal);
			var isCurrent = stamp.ContentHash == Skill.ContentHash;
			foreach (var path in stamp.Files.Keys.Union(carried.Keys, StringComparer.Ordinal))
			{
				carried.TryGetValue(path, out var carriedHash);
				var onDisk = SkillPaths.OnDisk(directory, path);
				if (!File.Exists(onDisk))
				{
					if (carriedHash != null)
						isCurrent = false;
					continue;
				}

				// a file is the installer's when it is what the stamp says or what the program carries - the second
				// .. is an update that was cut short, which the next one finishes
				var hash = SkillPaths.Hash(File.ReadAllBytes(onDisk));
				stamp.Files.TryGetValue(path, out var writtenHash);
				if (hash != writtenHash && hash != carriedHash)
					return new Inspection(SkillInstallationState.Modified, stamp);
				if (hash != carriedHash)
					isCurrent = false;
			}

			return new Inspection(isCurrent ? SkillInstallationState.Current : SkillInstallationState.Outdated, stamp);
		}
		catch (Exception ex) when (IsFileSystemFailure(ex))
		{
			// what cannot be read cannot be known as the installer's own
			return new Inspection(SkillInstallationState.Foreign, null);
		}
	}

	// the stamp of this owner, or null: none, not JSON, another owner's, or one whose paths could leave the directory
	private SkillStamp? ReadStamp(string directory)
	{
		var path = Path.Combine(directory, SkillPaths.StampFileName);
		if (!File.Exists(path))
			return null;

		SkillStamp? stamp;
		try
		{
			stamp = JsonSerializer.Deserialize(File.ReadAllBytes(path), SkillStampJsonContext.Default.SkillStamp);
		}
		catch (JsonException)
		{
			return null;
		}

		if (stamp == null || stamp.Files == null || stamp.Owner != owner)
			return null;
		if (stamp.Files.Any(x => x.Value == null || SkillPaths.TryNormalise(x.Key) != x.Key))
			return null;

		return stamp;
	}

	private SkillInstallResult Write(string directory, IReadOnlyList<SkillAgent> readBy, bool force, bool installWhenMissing)
	{
		try
		{
			var inspection = Inspect(directory);
			if (inspection.State == SkillInstallationState.Foreign)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.SkippedForeign);
			if (inspection.State == SkillInstallationState.Current)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.UpToDate);
			if (inspection.State == SkillInstallationState.Modified && !force)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.SkippedModified);
			if (inspection.State == SkillInstallationState.NotInstalled && !installWhenMissing)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.NotInstalled);

			var stamp = new SkillStamp
			{
				Owner = owner,
				Version = version,
				ContentHash = Skill.ContentHash,
				Files = Skill.Files.ToDictionary(x => x.RelativePath, x => x.Hash, StringComparer.Ordinal),
			};

			if (inspection.Stamp == null)
			{
				// the stamp goes first into a new directory, so an install that is cut short is still the installer's
				// .. own, and the next one finishes it
				Directory.CreateDirectory(directory);
				WriteStamp(directory, stamp);
			}

			foreach (var file in Skill.Files)
				WriteFile(SkillPaths.OnDisk(directory, file.RelativePath), file.Content.Span);

			if (inspection.Stamp != null)
			{
				// a file system that ignores case has one file for two spellings - never delete the one just written
				var written = new HashSet<string>(stamp.Files.Keys, StringComparer.OrdinalIgnoreCase);
				DeleteFiles(directory, inspection.Stamp.Files.Keys.Where(x => !written.Contains(x)));
			}
			WriteStamp(directory, stamp);

			return new SkillInstallResult(
				directory,
				readBy,
				inspection.Stamp == null ? SkillInstallOutcome.Installed : SkillInstallOutcome.Updated
			);
		}
		catch (Exception ex) when (IsFileSystemFailure(ex))
		{
			return new SkillInstallResult(directory, readBy, SkillInstallOutcome.Failed, ex.Message);
		}
	}

	private SkillInstallResult Delete(string directory, IReadOnlyList<SkillAgent> readBy, bool force)
	{
		try
		{
			var inspection = Inspect(directory);
			if (inspection.State == SkillInstallationState.NotInstalled)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.NotInstalled);
			if (inspection.Stamp == null)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.SkippedForeign);
			if (inspection.State == SkillInstallationState.Modified && !force)
				return new SkillInstallResult(directory, readBy, SkillInstallOutcome.SkippedModified);

			// only what the stamp lists - a file the user added beside the skill's own stays, and so does the directory
			DeleteFiles(directory, inspection.Stamp.Files.Keys);
			File.Delete(Path.Combine(directory, SkillPaths.StampFileName));
			if (!Directory.EnumerateFileSystemEntries(directory).Any())
				Directory.Delete(directory);

			return new SkillInstallResult(directory, readBy, SkillInstallOutcome.Removed);
		}
		catch (Exception ex) when (IsFileSystemFailure(ex))
		{
			return new SkillInstallResult(directory, readBy, SkillInstallOutcome.Failed, ex.Message);
		}
	}

	private static void WriteStamp(string directory, SkillStamp stamp)
	{
		WriteFile(
			Path.Combine(directory, SkillPaths.StampFileName),
			JsonSerializer.SerializeToUtf8Bytes(stamp, SkillStampJsonContext.Default.SkillStamp)
		);
	}

	// writes beside the file and moves it into place, so an agent never reads half a file
	private static void WriteFile(string path, ReadOnlySpan<byte> content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		var temporaryPath = path + TemporarySuffix;
		using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
			stream.Write(content);
		File.Move(temporaryPath, path, overwrite: true);
	}

	// deletes the skill's files at these paths, and each directory inside the skill's that this leaves empty
	private static void DeleteFiles(string directory, IEnumerable<string> relativePaths)
	{
		foreach (var relativePath in relativePaths.ToList())
		{
			var path = SkillPaths.OnDisk(directory, relativePath);
			File.Delete(path);

			var parent = Path.GetDirectoryName(path);
			while (parent != null && parent.Length > directory.Length && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
			{
				Directory.Delete(parent);
				parent = Path.GetDirectoryName(parent);
			}
		}
	}

	private static bool IsFileSystemFailure(Exception ex)
	{
		return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException || ex is NotSupportedException;
	}

	#endregion
}
