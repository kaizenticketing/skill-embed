# Kaizen.SkillEmbed

Ship an [agent skill](https://agentskills.io/) inside a .NET command line, and have the command line install it.

Your tool carries its skill as embedded resources. `Kaizen.SkillEmbed` finds the AI coding agents on the user's machine - Claude Code, Codex, Cursor, Antigravity and [70 more](docs/supported-agents.md) - copies the skill into each one's skills directory, and brings those copies up to date when the tool itself is updated. The skill can never be a different version from the binary that explains it.

- No dependencies, and it works in a Native AOT binary.
- .NET 8 and .NET 10.
- It writes only where an agent already is, never touches a directory it did not write, and leaves a copy the user has edited alone.

## Install

```bash
dotnet add package Kaizen.SkillEmbed
```

## Use

**1. Embed the skill.** Keep it in your project as a directory with a `SKILL.md`, and give each file a logical name that keeps its path - the default name turns every slash into a dot:

```xml
<ItemGroup>
  <EmbeddedResource Include="Skills\my-tool\**\*" LogicalName="skills/my-tool/%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

**2. Install it from a command of your tool,** such as `my-tool agent setup`:

```csharp
using Kaizen.SkillEmbed;

var skill = EmbeddedSkill.FromAssembly(typeof(Program).Assembly, "my-tool", "skills/my-tool/");
var installer = new SkillInstaller(skill, owner: "my-tool", version: "1.4.0");

var results = installer.Install();
foreach (var result in results)
    Console.WriteLine($"{result.Outcome,-16} {result.Directory}  ({string.Join(", ", result.Agents.Select(x => x.DisplayName))})");

// keep what is yours, in your tool's own config
config.SkillHash = skill.ContentHash;
config.SkillDirectories = results
    .Where(x => x.Outcome != SkillInstallOutcome.Failed && x.Outcome != SkillInstallOutcome.SkippedForeign)
    .Select(x => x.Directory)
    .ToList();
```

**3. Refresh at the start of every command.** It costs one string comparison until the tool is updated with a changed skill:

```csharp
if (config.SkillHash != null && config.SkillHash != skill.ContentHash)
{
    foreach (var result in installer.Refresh(config.SkillDirectories))
    {
        if (result.Outcome == SkillInstallOutcome.SkippedModified)
            Console.Error.WriteLine($"my-tool: {result.Directory} has been edited, so it was not updated. Run my-tool agent setup --force to replace it.");
    }
    config.SkillHash = skill.ContentHash;
}
```

`Refresh` only ever looks at the directories you give it, so it finds no new agents and it installs nothing new: that is what the setup command is for.

## What it does to the user's machine

| | |
| :--- | :--- |
| **Where it writes** | `<the agent's skills directory>/<the skill's name>/`, for each agent whose own directory exists - `~/.claude/skills/my-tool/` for Claude Code. Agents that read the shared `~/.agents/skills` get one copy between them. |
| **The stamp** | Each copy gets a `.skill-embed.json` with your tool's name, its version and a hash of every file it wrote. That is how the installer knows a directory as its own. |
| **A directory without your stamp** | Never touched - not by `Install`, not by `Remove`, not with `force`. It is the user's own skill of the same name, or another tool's. |
| **A file the user changed** | The copy is left alone and comes back as `SkippedModified`. `force: true` overwrites it. |
| **A file the user added** | Stays. `Remove` deletes the files the stamp lists, and the directory only when nothing else is in it. |
| **A file system that refuses** | An outcome, `Failed` with the message - never an exception. A sandbox or a read-only home cannot stop your command. |
| **An update that was cut short** | The next install or refresh finishes it. |

## The API

| | |
| :--- | :--- |
| `EmbeddedSkill.FromAssembly(assembly, name, resourcePrefix)` | The skill, from embedded resources. Or `new EmbeddedSkill(name, files)` from bytes you hold. |
| `skill.ContentHash` | Changes when the skill does, and only then. |
| `installer.DetectAgents()` | The agents on the machine. |
| `installer.GetInstallations()` | For each place the skill is or would go: `NotInstalled`, `Current`, `Outdated`, `Modified` or `Foreign`, and the version that wrote it. Writes nothing - this is your `status` command. |
| `installer.Install(agents, force)` | Installs or updates. Default: every detected agent. |
| `installer.Refresh(directories)` | Updates the copies in these directories. |
| `installer.Remove(agents, force)` | Removes what the installer wrote. |

`SkillInstaller` takes a `SkillEnvironment` - the home directory, the platform and the environment variables - so your tests can run it against a temporary directory.

## Agents

[docs/supported-agents.md](docs/supported-agents.md) lists every agent, its skills directory and what shows that it is installed. An agent's own environment variable is obeyed: with `CLAUDE_CONFIG_DIR` set, Claude Code's skills go there.

To install into only some agents, name them:

```csharp
installer.Install([SkillAgents.Find("claude-code")!, SkillAgents.Find("codex")!]);
```

To add an agent the table does not have, describe it - a path is a template, with `{home}` and `{env:NAME}`:

```csharp
var mine = new SkillAgent("my-agent", "My Agent", skillsDirectory: "{home}/.my-agent/skills", "{home}/.my-agent");
var installer = new SkillInstaller(skill, "my-tool", "1.4.0", agents: [.. SkillAgents.All, mine]);
```

### Where the table comes from

The table is not maintained by hand. `scripts/generate-agents.mjs` generates it from the agent table of [vercel-labs/skills](https://github.com/vercel-labs/skills), the `npx skills` installer, which a great many people keep correct. A weekly workflow runs the script against their newest release and opens a pull request when a row changed.

So a wrong or a missing agent is best fixed [there](https://github.com/vercel-labs/skills): it reaches this package with the next sync, and everyone who uses `npx skills` gets it too.

The script leaves out an agent that a single test for a path cannot describe, and `docs/supported-agents.md` says which and why.

## Develop

```bash
dotnet build
dotnet test

# regenerate the agent table - needs node 22.13 or newer
git clone --depth 1 https://github.com/vercel-labs/skills ../vercel-skills
node scripts/generate-agents.mjs ../vercel-skills
```

A GitHub Release named `vX.Y.Z` publishes that version to nuget.org.

## Thanks

- [vercel-labs/skills](https://github.com/vercel-labs/skills) for the agent table (MIT - see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)).
- The [Aspire CLI](https://github.com/microsoft/aspire), whose `aspire agent init` showed what a .NET command line needs here - and that a copy with no stamp cannot later be told from the user's own.

## Licence

[MIT](LICENSE). Copyright (c) 2026 Kaizen Ticketing Solutions LTD.
