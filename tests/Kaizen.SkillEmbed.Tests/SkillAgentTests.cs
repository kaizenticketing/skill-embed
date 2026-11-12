namespace Kaizen.SkillEmbed.Tests;

public class SkillAgentTests
{
	[Theory]
	[InlineData(SkillPlatform.Linux)]
	[InlineData(SkillPlatform.MacOS)]
	[InlineData(SkillPlatform.Windows)]
	public void Every_agent_of_the_table_has_a_skills_directory_and_is_not_on_an_empty_machine(SkillPlatform platform)
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		var environment = home.On(platform);

		// *** act & assert ***
		Assert.NotEmpty(SkillAgents.All);
		Assert.Equal(SkillAgents.All.Count, SkillAgents.All.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
		Assert.All(SkillAgents.All, agent =>
		{
			Assert.False(string.IsNullOrWhiteSpace(agent.DisplayName));
			Assert.False(agent.IsInstalled(environment), agent.Name);
			var skillsDirectory = agent.GetSkillsDirectory(environment);
			Assert.NotNull(skillsDirectory);
			// with no variable set every agent's skills are in the user's home
			Assert.StartsWith(home.Path, skillsDirectory, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void An_agent_follows_its_environment_variable_and_its_platform()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		var claude = SkillAgents.Find("Claude-Code")!;
		var reasonix = SkillAgents.Find("reasonix")!;
		Assert.Null(SkillAgents.Find("no-such-agent"));

		// *** act & assert ***
		// the default - ~/.claude
		Assert.Equal(home.At(".claude", "skills"), claude.GetSkillsDirectory(home.On()));
		Assert.False(claude.IsInstalled(home.On()));
		home.CreateDirectory(".claude");
		Assert.True(claude.IsInstalled(home.On()));

		// CLAUDE_CONFIG_DIR moves claude code: its skills go there, and ~/.claude no longer shows that it is installed
		home.SetVariable("CLAUDE_CONFIG_DIR", home.At("elsewhere", "claude"));
		Assert.Equal(home.At("elsewhere", "claude", "skills"), claude.GetSkillsDirectory(home.On()));
		Assert.False(claude.IsInstalled(home.On()));
		home.CreateDirectory("elsewhere", "claude");
		Assert.True(claude.IsInstalled(home.On()));

		// a variable that is only white space is not set
		home.SetVariable("CLAUDE_CONFIG_DIR", "  ");
		Assert.Equal(home.At(".claude", "skills"), claude.GetSkillsDirectory(home.On()));

		// an agent whose directory is elsewhere on windows, and under APPDATA when that is set
		Assert.Equal(home.At(".reasonix", "skills"), reasonix.GetSkillsDirectory(home.On(SkillPlatform.Linux)));
		Assert.Equal(home.At("AppData", "Roaming", "reasonix", "skills"), reasonix.GetSkillsDirectory(home.On(SkillPlatform.Windows)));
		home.SetVariable("APPDATA", home.At("roaming"));
		Assert.Equal(home.At("roaming", "reasonix", "skills"), reasonix.GetSkillsDirectory(home.On(SkillPlatform.Windows)));
		Assert.Equal(home.At(".reasonix", "skills"), reasonix.GetSkillsDirectory(home.On(SkillPlatform.Linux)));
	}

	[Fact]
	public void An_agent_of_your_own_is_found_by_any_of_its_paths()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		var agent = new SkillAgent("mine", "Mine", "{env:MINE_HOME}/skills", "{home}/.mine", "{home}/.config/mine.json");

		// *** act & assert ***
		// its skills directory hangs on a variable that is not set, so an installer has nowhere to put a skill
		Assert.Null(agent.GetSkillsDirectory(home.On()));
		Assert.False(agent.IsInstalled(home.On()));
		home.WriteFile("{}", ".config", "mine.json");
		Assert.True(agent.IsInstalled(home.On()));
		var installer = new SkillInstaller(TemporaryHome.Skill(("SKILL.md", "ours")), "tool", "1.0.0", home.On(), [agent]);
		Assert.Empty(installer.Install());

		home.SetVariable("MINE_HOME", home.At("mine"));
		Assert.Equal(home.At("mine", "skills", "sample"), installer.Install().Single().Directory);

		Assert.Throws<FormatException>(() => new SkillAgent("bad", "Bad", "{cwd}/skills", "{home}/.bad").GetSkillsDirectory(home.On()));
		Assert.Throws<ArgumentException>(() => new SkillAgent("bad", "Bad", "{home}/skills"));
	}
}
