namespace KaizenTicketing.SkillEmbed;

/// <summary>
/// One file of an <see cref="EmbeddedSkill"/>.
/// </summary>
public sealed class EmbeddedSkillFile
{
	/// <summary>Makes a file.</summary>
	/// <param name="relativePath">Its path inside the skill's directory, such as <c>references/commands.md</c>.</param>
	/// <param name="content">Its bytes, which are installed as they are.</param>
	/// <exception cref="ArgumentException">The path could leave the skill's directory.</exception>
	public EmbeddedSkillFile(string relativePath, ReadOnlyMemory<byte> content)
	{
		ArgumentNullException.ThrowIfNull(relativePath);

		RelativePath = SkillPaths.TryNormalise(relativePath)
			?? throw new ArgumentException($"'{relativePath}' is not a path inside a skill's directory.", nameof(relativePath));
		Content = content;
		Hash = SkillPaths.Hash(content.Span);
	}

	/// <summary>The file's path inside the skill's directory, with forward slashes.</summary>
	public string RelativePath { get; }

	/// <summary>The file's bytes.</summary>
	public ReadOnlyMemory<byte> Content { get; }

	internal string Hash { get; }
}
