using System.Text;

namespace KaizenTicketing.SkillEmbed.Tests;

/// <summary>
/// A home directory of a test's own, which goes when the test does.
/// </summary>
internal sealed class TemporaryHome : IDisposable
{
	private readonly Dictionary<string, string> variables = new(StringComparer.Ordinal);

	public TemporaryHome()
	{
		Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"skill-embed-{Guid.NewGuid():N}");
		Directory.CreateDirectory(Path);
	}

	public string Path { get; }

	/// <summary>The environment over this home, with the variables set so far.</summary>
	public SkillEnvironment On(SkillPlatform platform = SkillPlatform.Linux)
	{
		return new SkillEnvironment(Path, platform, name => variables.GetValueOrDefault(name));
	}

	public void SetVariable(string name, string value)
	{
		variables[name] = value;
	}

	/// <summary>A path in this home from its parts.</summary>
	public string At(params string[] parts)
	{
		return System.IO.Path.Combine([Path, .. parts]);
	}

	public void CreateDirectory(params string[] parts)
	{
		Directory.CreateDirectory(At(parts));
	}

	public void WriteFile(string content, params string[] parts)
	{
		var path = At(parts);
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	public string ReadFile(params string[] parts)
	{
		return File.ReadAllText(At(parts));
	}

	/// <summary>A skill named <c>sample</c> from (path, content) pairs.</summary>
	public static EmbeddedSkill Skill(params (string Path, string Content)[] files)
	{
		return new EmbeddedSkill("sample", files.Select(x => new EmbeddedSkillFile(x.Path, Encoding.UTF8.GetBytes(x.Content))));
	}

	public void Dispose()
	{
		if (Directory.Exists(Path))
			Directory.Delete(Path, recursive: true);
	}
}
