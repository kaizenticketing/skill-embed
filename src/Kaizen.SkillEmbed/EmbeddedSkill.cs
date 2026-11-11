using System.Reflection;
using System.Text;

namespace Kaizen.SkillEmbed;

/// <summary>
/// A skill a program carries inside itself: a name and its files, of which <c>SKILL.md</c> is one.
/// </summary>
public sealed class EmbeddedSkill
{
	/// <summary>The file every skill has at the top of its directory.</summary>
	public const string EntryFileName = "SKILL.md";

	/// <summary>Makes a skill from files the program holds.</summary>
	/// <param name="name">
	/// The skill's name, which is its directory's name in every agent: lower case letters, digits and hyphens. It should
	/// be the <c>name</c> in SKILL.md's front matter.
	/// </param>
	/// <param name="files">The skill's files.</param>
	/// <exception cref="ArgumentException">The name is not a skill's name, a path is there twice, or there is no SKILL.md.</exception>
	public EmbeddedSkill(string name, IEnumerable<EmbeddedSkillFile> files)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(files);
		if (!SkillPaths.IsValidName(name))
			throw new ArgumentException($"'{name}' is not a skill's name: lower case letters, digits and single hyphens, at most 64.", nameof(name));

		var sorted = files.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToList();
		// agents on Windows and macOS do not tell two paths apart by case
		var duplicate = sorted.GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
		if (duplicate != null)
			throw new ArgumentException($"The skill has the file '{duplicate.Key}' twice.", nameof(files));
		if (!sorted.Any(x => x.RelativePath == EntryFileName))
			throw new ArgumentException($"The skill has no {EntryFileName}.", nameof(files));

		Name = name;
		Files = sorted;
		ContentHash = HashOf(sorted);
	}

	/// <summary>The skill's name, and its directory's name in every agent.</summary>
	public string Name { get; }

	/// <summary>The skill's files, in the order of their paths.</summary>
	public IReadOnlyList<EmbeddedSkillFile> Files { get; }

	/// <summary>
	/// A hash of every file's path and content. It changes when the skill does, and only then - keep it beside the
	/// directories you installed into, to know when to <see cref="SkillInstaller.Refresh"/>.
	/// </summary>
	public string ContentHash { get; }

	/// <summary>
	/// Reads a skill from an assembly's embedded resources: every resource whose name starts with the prefix is a
	/// file, and the rest of its name is the file's path.
	/// </summary>
	/// <remarks>
	/// Give each resource a logical name that keeps its path, because the default name turns every slash into a dot:
	/// <code>
	/// &lt;EmbeddedResource Include="Skills\my-tool\**\*" LogicalName="skills/my-tool/%(RecursiveDir)%(Filename)%(Extension)" /&gt;
	/// </code>
	/// and read it with <c>EmbeddedSkill.FromAssembly(typeof(Program).Assembly, "my-tool", "skills/my-tool/")</c>.
	/// </remarks>
	/// <param name="assembly">The assembly that holds the resources.</param>
	/// <param name="name">The skill's name.</param>
	/// <param name="resourcePrefix">What every resource name of the skill starts with, such as <c>skills/my-tool/</c>.</param>
	/// <exception cref="ArgumentException">No resource has the prefix, or the files do not make a skill.</exception>
	public static EmbeddedSkill FromAssembly(Assembly assembly, string name, string resourcePrefix)
	{
		ArgumentNullException.ThrowIfNull(assembly);
		ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);

		var files = new List<EmbeddedSkillFile>();
		foreach (var resourceName in assembly.GetManifestResourceNames())
		{
			if (!resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal))
				continue;

			using var stream = assembly.GetManifestResourceStream(resourceName)
				?? throw new ArgumentException($"The resource '{resourceName}' cannot be read.", nameof(assembly));
			using var content = new MemoryStream();
			stream.CopyTo(content);
			files.Add(new EmbeddedSkillFile(resourceName.Substring(resourcePrefix.Length), content.ToArray()));
		}

		if (files.Count == 0)
			throw new ArgumentException($"{assembly.GetName().Name} has no resource whose name starts with '{resourcePrefix}'.", nameof(resourcePrefix));

		return new EmbeddedSkill(name, files);
	}

	#region Internal Implementation

	private static string HashOf(List<EmbeddedSkillFile> files)
	{
		var listing = new StringBuilder();
		foreach (var file in files)
			listing.Append(file.RelativePath).Append('\0').Append(file.Hash).Append('\n');

		return SkillPaths.Hash(Encoding.UTF8.GetBytes(listing.ToString()));
	}

	#endregion
}
