namespace Kaizen.SkillEmbed.Tests;

public class SkillInstallerTests
{
	[Fact]
	public void Install_refresh_and_remove_a_skill_across_the_agents_on_a_machine()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		// claude code and antigravity have a skills directory of their own - codex and cursor share ~/.agents/skills
		home.CreateDirectory(".claude");
		home.CreateDirectory(".gemini", "antigravity");
		home.CreateDirectory(".codex");
		home.CreateDirectory(".cursor");

		var claude = home.At(".claude", "skills", "sample");
		var antigravity = home.At(".gemini", "config", "skills", "sample");
		var shared = home.At(".agents", "skills", "sample");

		var first = TemporaryHome.Skill(
			("SKILL.md", "first"),
			("references/commands.md", "commands"),
			("references/old/gone.md", "gone in the second")
		);
		// the whole table finds these four, and gemini cli by ~/.gemini - the installers here look for the four, and
		// .. for one agent that is not on the machine
		var agents = new[] { "claude-code", "antigravity", "codex", "cursor", "amp" }.Select(x => SkillAgents.Find(x)!).ToList();
		var installer = new SkillInstaller(first, "tool", "1.0.0", home.On(), agents);

		// *** act & assert ***
		// install - one copy for each directory, and the agents that share one share a result
		Assert.Equal(
			["antigravity", "claude-code", "codex", "cursor", "gemini-cli"],
			new SkillInstaller(first, "tool", "1.0.0", home.On()).DetectAgents().Select(x => x.Name).Order());
		Assert.Equal(["antigravity", "claude-code", "codex", "cursor"], installer.DetectAgents().Select(x => x.Name).Order());

		var installed = installer.Install();
		Assert.Equal([shared, claude, antigravity], installed.Select(x => x.Directory).Order(StringComparer.Ordinal));
		Assert.All(installed, x => Assert.Equal(SkillInstallOutcome.Installed, x.Outcome));
		Assert.Equal(["codex", "cursor"], installed.Single(x => x.Directory == shared).Agents.Select(x => x.Name).Order());
		Assert.Equal("first", home.ReadFile(".claude", "skills", "sample", "SKILL.md"));
		Assert.Equal("commands", home.ReadFile(".agents", "skills", "sample", "references", "commands.md"));
		Assert.Equal("gone in the second", home.ReadFile(".gemini", "config", "skills", "sample", "references", "old", "gone.md"));

		Assert.All(installer.GetInstallations(), x => Assert.Equal(SkillInstallationState.Current, x.State));
		Assert.All(installer.Install(), x => Assert.Equal(SkillInstallOutcome.UpToDate, x.Outcome));

		// the program is updated with a changed skill - a refresh of the directories it kept brings each copy up
		var second = TemporaryHome.Skill(
			("SKILL.md", "second"),
			("references/commands.md", "commands"),
			("scripts/new.sh", "new in the second")
		);
		installer = new SkillInstaller(second, "tool", "2.0.0", home.On(), agents);
		Assert.NotEqual(first.ContentHash, second.ContentHash);
		Assert.All(installer.GetInstallations(), x =>
		{
			Assert.Equal(SkillInstallationState.Outdated, x.State);
			Assert.Equal("1.0.0", x.InstalledVersion);
		});

		var directories = installed.Select(x => x.Directory).ToList();
		Assert.All(installer.Refresh(directories), x => Assert.Equal(SkillInstallOutcome.Updated, x.Outcome));
		Assert.Equal("second", home.ReadFile(".claude", "skills", "sample", "SKILL.md"));
		Assert.Equal("new in the second", home.ReadFile(".claude", "skills", "sample", "scripts", "new.sh"));
		// the file the second skill no longer has goes, with the directory that held only it
		Assert.False(Directory.Exists(Path.Combine(claude, "references", "old")));
		Assert.True(File.Exists(Path.Combine(claude, "references", "commands.md")));
		Assert.All(installer.GetInstallations(), x =>
		{
			Assert.Equal(SkillInstallationState.Current, x.State);
			Assert.Equal("2.0.0", x.InstalledVersion);
		});
		Assert.All(installer.Refresh(directories), x => Assert.Equal(SkillInstallOutcome.UpToDate, x.Outcome));

		// the user changes one copy and adds a file of their own to another - the next refresh leaves the changed
		// .. copy alone, and only a forced install overwrites it
		home.WriteFile("the user's own words", ".claude", "skills", "sample", "SKILL.md");
		home.WriteFile("the user's own file", ".agents", "skills", "sample", "notes.md");
		var third = TemporaryHome.Skill(("SKILL.md", "third"), ("references/commands.md", "commands"));
		installer = new SkillInstaller(third, "tool", "3.0.0", home.On(), agents);

		Assert.Equal(SkillInstallationState.Modified, installer.GetInstallations().Single(x => x.Directory == claude).State);
		var refreshed = installer.Refresh(directories);
		Assert.Equal(SkillInstallOutcome.SkippedModified, refreshed.Single(x => x.Directory == claude).Outcome);
		Assert.Equal(SkillInstallOutcome.Updated, refreshed.Single(x => x.Directory == shared).Outcome);
		Assert.Equal(SkillInstallOutcome.Updated, refreshed.Single(x => x.Directory == antigravity).Outcome);
		Assert.Equal("the user's own words", home.ReadFile(".claude", "skills", "sample", "SKILL.md"));
		Assert.Equal(SkillInstallOutcome.SkippedModified, installer.Install().Single(x => x.Directory == claude).Outcome);
		Assert.Equal(SkillInstallOutcome.SkippedModified, installer.Remove([agents[0]]).Single().Outcome);
		Assert.Equal("the user's own words", home.ReadFile(".claude", "skills", "sample", "SKILL.md"));
		Assert.Equal(SkillInstallOutcome.Updated, installer.Install(force: true).Single(x => x.Directory == claude).Outcome);
		Assert.Equal("third", home.ReadFile(".claude", "skills", "sample", "SKILL.md"));

		// remove - the installer's files go, and a directory goes with them unless the user has a file in it
		Assert.All(installer.Remove(), x => Assert.Equal(SkillInstallOutcome.Removed, x.Outcome));
		Assert.False(Directory.Exists(claude));
		Assert.False(Directory.Exists(antigravity));
		Assert.Equal(["notes.md"], Directory.EnumerateFileSystemEntries(shared).Select(Path.GetFileName));
		Assert.Equal(SkillInstallOutcome.NotInstalled, installer.Refresh([claude]).Single().Outcome);
		Assert.False(Directory.Exists(claude));
	}

	[Fact]
	public void A_directory_the_installer_did_not_write_is_never_touched()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		var skill = TemporaryHome.Skill(("SKILL.md", "ours"));
		var theirs = new SkillAgent("theirs", "Theirs", "{home}/.theirs/skills", "{home}/.theirs");
		var installer = new SkillInstaller(skill, "tool", "1.0.0", home.On(), [theirs]);
		var directory = home.At(".theirs", "skills", "sample");

		// *** act & assert ***
		// no stamp - the user's own skill of the same name
		home.WriteFile("the user's own skill", ".theirs", "skills", "sample", "SKILL.md");
		AssertUntouched();

		// another program's stamp
		home.WriteFile(
			"""{ "owner": "another-tool", "version": "9", "contentHash": "x", "files": { "SKILL.md": "x" } }""",
			".theirs", "skills", "sample", ".skill-embed.json");
		AssertUntouched();

		// our owner's name on a stamp whose path leaves the directory - a remove must not follow it
		home.WriteFile("outside", ".theirs", "outside.md");
		home.WriteFile(
			"""{ "owner": "tool", "version": "1", "contentHash": "x", "files": { "../../outside.md": "x" } }""",
			".theirs", "skills", "sample", ".skill-embed.json");
		AssertUntouched();
		Assert.Equal("outside", home.ReadFile(".theirs", "outside.md"));

		// a stamp that is not JSON
		home.WriteFile("{ not json", ".theirs", "skills", "sample", ".skill-embed.json");
		AssertUntouched();

		void AssertUntouched()
		{
			Assert.Equal(SkillInstallationState.Foreign, installer.GetInstallations().Single().State);
			Assert.Equal(SkillInstallOutcome.SkippedForeign, installer.Install(force: true).Single().Outcome);
			Assert.Equal(SkillInstallOutcome.SkippedForeign, installer.Refresh([directory]).Single().Outcome);
			Assert.Equal(SkillInstallOutcome.SkippedForeign, installer.Remove(force: true).Single().Outcome);
			Assert.Equal("the user's own skill", home.ReadFile(".theirs", "skills", "sample", "SKILL.md"));
		}
	}

	[Fact]
	public void An_install_that_was_cut_short_is_finished_by_the_next_one()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		home.CreateDirectory(".claude");
		var first = TemporaryHome.Skill(("SKILL.md", "first"), ("references/commands.md", "first commands"));
		var second = TemporaryHome.Skill(("SKILL.md", "second"), ("references/commands.md", "second commands"));
		var directory = new SkillInstaller(first, "tool", "1.0.0", home.On()).Install().Single().Directory;
		var installer = new SkillInstaller(second, "tool", "2.0.0", home.On());

		// *** act & assert ***
		// an update stopped after its first file: one file is the second skill's, one the first's, the stamp the first's
		home.WriteFile("second", ".claude", "skills", "sample", "SKILL.md");
		Assert.Equal(SkillInstallationState.Outdated, installer.GetInstallations().Single().State);
		Assert.Equal(SkillInstallOutcome.Updated, installer.Refresh([directory]).Single().Outcome);
		Assert.Equal("second commands", home.ReadFile(".claude", "skills", "sample", "references", "commands.md"));

		// a file that is lost is put back
		File.Delete(home.At(".claude", "skills", "sample", "SKILL.md"));
		Assert.Equal(SkillInstallationState.Outdated, installer.GetInstallations().Single().State);
		Assert.Equal(SkillInstallOutcome.Updated, installer.Refresh([directory]).Single().Outcome);
		Assert.Equal(SkillInstallationState.Current, installer.GetInstallations().Single().State);
	}

	[Fact]
	public void A_file_system_that_refuses_is_an_outcome_not_an_exception()
	{
		// *** arrange ***
		using var home = new TemporaryHome();
		home.CreateDirectory(".claude");
		// a file where the skills directory would be - nothing can be made inside it
		home.WriteFile("in the way", ".claude", "skills");
		var installer = new SkillInstaller(TemporaryHome.Skill(("SKILL.md", "ours")), "tool", "1.0.0", home.On());

		// *** act & assert ***
		var result = installer.Install().Single();
		Assert.Equal(SkillInstallOutcome.Failed, result.Outcome);
		Assert.False(string.IsNullOrWhiteSpace(result.Error));
		Assert.Equal("in the way", home.ReadFile(".claude", "skills"));
	}
}
