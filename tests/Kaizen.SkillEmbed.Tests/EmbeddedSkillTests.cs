using System.Text;

namespace Kaizen.SkillEmbed.Tests;

public class EmbeddedSkillTests
{
	[Fact]
	public void A_skill_is_read_from_an_assembly_with_the_paths_of_its_files()
	{
		// *** act ***
		var skill = EmbeddedSkill.FromAssembly(typeof(EmbeddedSkillTests).Assembly, "sample", "skills/sample/");

		// *** assert ***
		Assert.Equal("sample", skill.Name);
		Assert.Equal(["SKILL.md", "references/commands.md"], skill.Files.Select(x => x.RelativePath));
		Assert.StartsWith("---\nname: sample\n", Encoding.UTF8.GetString(skill.Files[0].Content.Span).ReplaceLineEndings("\n"), StringComparison.Ordinal);

		// the hash is the content's - the same from the same files in any order, and another when a file changes
		var files = skill.Files.Reverse().ToList();
		Assert.Equal(skill.ContentHash, new EmbeddedSkill("sample", files).ContentHash);
		files[0] = new EmbeddedSkillFile("references/commands.md", "changed"u8.ToArray());
		Assert.NotEqual(skill.ContentHash, new EmbeddedSkill("sample", files).ContentHash);

		Assert.Throws<ArgumentException>(() => EmbeddedSkill.FromAssembly(typeof(EmbeddedSkillTests).Assembly, "sample", "skills/no-such-skill/"));
	}

	[Theory]
	[InlineData("Sample")]
	[InlineData("my_skill")]
	[InlineData("-sample")]
	[InlineData("sample-")]
	[InlineData("my--skill")]
	[InlineData("../sample")]
	[InlineData("")]
	public void A_name_that_could_not_be_a_skills_directory_is_refused(string name)
	{
		Assert.Throws<ArgumentException>(() => new EmbeddedSkill(name, [new EmbeddedSkillFile("SKILL.md", default)]));
	}

	[Theory]
	[InlineData("../SKILL.md")]
	[InlineData("/etc/passwd")]
	[InlineData("references/../../SKILL.md")]
	[InlineData("C:\\Users\\SKILL.md")]
	[InlineData("references//commands.md")]
	[InlineData(".skill-embed.json")]
	public void A_path_that_could_leave_the_skills_directory_is_refused(string path)
	{
		Assert.Throws<ArgumentException>(() => new EmbeddedSkillFile(path, default));
	}

	[Fact]
	public void A_skill_needs_its_entry_file_and_each_path_once()
	{
		Assert.Throws<ArgumentException>(() => new EmbeddedSkill("sample", [new EmbeddedSkillFile("README.md", default)]));
		Assert.Throws<ArgumentException>(() => new EmbeddedSkill("sample", [
			new EmbeddedSkillFile("SKILL.md", default),
			new EmbeddedSkillFile("references/Commands.md", default),
			new EmbeddedSkillFile("references\\commands.md", default),
		]));
	}
}
