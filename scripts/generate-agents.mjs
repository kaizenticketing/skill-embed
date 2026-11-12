#!/usr/bin/env node
// generates the agent table from the agent table of https://github.com/vercel-labs/skills (MIT, see
// THIRD-PARTY-NOTICES.txt), so nobody maintains our rows by hand
//
//   node scripts/generate-agents.mjs <path to a checkout of vercel-labs/skills>
//
// it does not parse src/agents.ts: it runs it, in a made-up world whose home directory, environment variables and
// file system are placeholders. what each agent then asks for is its row - the directory its skills go in, and the
// paths whose existence says the agent is installed - with the placeholders left in as {home} and {env:NAME}
//
// it writes:
//   src/KaizenTicketing.SkillEmbed/Agents/agents.json   the table the library embeds
//   docs/supported-agents.md                   the same table for people, and the agents left out with the reason
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { stripTypeScriptTypes } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const REPOSITORY = "https://github.com/vercel-labs/skills";
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const TABLE_PATH = path.join(ROOT, "src", "KaizenTicketing.SkillEmbed", "Agents", "agents.json");
const DOCUMENT_PATH = path.join(ROOT, "docs", "supported-agents.md");

const HOME = "/@@home@@";
const CWD = "/@@cwd@@";
const PLATFORMS = ["linux", "darwin", "win32"];
// the name our table gives each of node's platforms
const PLATFORM_NAMES = { linux: "linux", darwin: "macos", win32: "windows" };

// what src/agents.ts may import - anything else is a change to its structure that this script must be taught
const KNOWN_IMPORTS = {
	os: ["homedir", "platform"],
	path: ["join"],
	fs: ["existsSync", "readFileSync", "readdirSync"],
	"xdg-basedir": ["xdgConfig"],
	"./types.ts": ["AgentConfig", "AgentType"],
};

// rows of the upstream table that are not an agent
const NOT_AN_AGENT = new Set(["universal"]);

const upstream = process.argv[2];
if (!upstream) {
	console.error("usage: node scripts/generate-agents.mjs <path to a checkout of vercel-labs/skills>");
	process.exit(2);
}

const source = {
	repository: REPOSITORY,
	version: JSON.parse(readFileSync(path.join(upstream, "package.json"), "utf8")).version,
	commit: execFileSync("git", ["-C", upstream, "rev-parse", "HEAD"], { encoding: "utf8" }).trim(),
};
const evaluate = compile(readFileSync(path.join(upstream, "src", "agents.ts"), "utf8"));

// every environment variable the table reads, on any platform
const variables = new Set();
for (const platform of PLATFORMS) {
	for (const name of (await run(platform, () => true)).variablesRead) variables.add(name);
}

const byPlatform = {};
for (const platform of PLATFORMS) byPlatform[platform] = await describePlatform(platform);

const agents = [];
const excluded = [];
for (const name of Object.keys(byPlatform.linux)) {
	if (NOT_AN_AGENT.has(name)) continue;

	const rows = PLATFORMS.map((platform) => byPlatform[platform][name]);
	const reason = rows.map((row) => row.excluded).find((x) => x);
	if (reason) {
		excluded.push({ name, displayName: rows[0].displayName, reason });
		continue;
	}

	const [linux, ...others] = rows.map((row) => ({ skillsDirectory: row.skillsDirectory, detect: row.detect }));
	const agent = { name, displayName: rows[0].displayName, ...linux };
	others.forEach((row, index) => {
		if (JSON.stringify(row) !== JSON.stringify(linux)) agent[PLATFORM_NAMES[PLATFORMS[index + 1]]] = row;
	});
	agents.push(agent);
}

writeFileSync(TABLE_PATH, JSON.stringify({ source, agents, excluded }, null, "\t") + "\n");
writeFileSync(DOCUMENT_PATH, renderDocument());
console.log(`${agents.length} agents, ${excluded.length} left out, from ${REPOSITORY} ${source.version} (${source.commit.slice(0, 7)})`);

// turns src/agents.ts into a function of the world it runs in
function compile(typescript) {
	const imports = /^import\s+(?:type\s+)?\{([^}]*)\}\s+from\s+'([^']+)';?\s*$/gm;
	for (const [, names, module] of typescript.matchAll(imports)) {
		const known = KNOWN_IMPORTS[module];
		if (!known) throw new Error(`src/agents.ts imports '${module}', which this script does not know`);
		for (const name of names.split(",").map((x) => x.trim()).filter((x) => x)) {
			if (!known.includes(name)) throw new Error(`src/agents.ts imports ${name} from '${module}', which this script does not know`);
		}
	}

	const body = stripTypeScriptTypes(typescript.replace(imports, ""), { mode: "strip" }).replace(/^export\s+/gm, "");
	if (/^\s*import\s/m.test(body)) throw new Error("src/agents.ts has an import this script could not read");

	// a context of its own keeps this process's globals away from the upstream code. node does not call that a
	// .. security boundary, so the sync workflow also runs this script in a job that holds no write token
	return vm.compileFunction(
		`${body}\nreturn { agents, readsCanonicalGlobally };`,
		["homedir", "platform", "join", "existsSync", "readFileSync", "readdirSync", "xdgConfig", "process"],
		{ parsingContext: vm.createContext(Object.create(null)) }
	);
}

// runs the table on one platform. isSet says which environment variables have a value - the value is a placeholder
async function run(platform, isSet) {
	const variablesRead = new Set();
	const env = new Proxy({}, {
		get(_, name) {
			if (typeof name !== "string") return undefined;
			variablesRead.add(name);
			return isSet(name) ? `/@@env:${name}@@` : undefined;
		},
	});

	const world = { probes: [], exists: () => false, readsFiles: false };
	const refuse = () => {
		world.readsFiles = true;
		throw new Error("no file system here");
	};
	// xdg-basedir's own rule
	const xdgConfig = env.XDG_CONFIG_HOME || path.posix.join(HOME, ".config");
	const load = (exists) => {
		world.probes = [];
		world.exists = exists;
		return evaluate(
			() => HOME,
			() => platform,
			path.posix.join,
			(probe) => {
				world.probes.push(probe);
				return world.exists(probe);
			},
			refuse,
			refuse,
			xdgConfig,
			{ env, platform, cwd: () => CWD }
		);
	};
	const skillsDirectoryOf = (loaded, name) => loaded.readsCanonicalGlobally(name)
		? path.posix.join(HOME, ".agents", "skills")
		: loaded.agents[name].globalSkillsDir;

	const table = load(() => false);

	// a few agents choose their skills directory by what is on disk (OpenClaw, by which of its names it was
	// .. installed under) - the paths the table itself asks for while it loads show which
	const choosesByDisk = new Set();
	for (const probe of [...world.probes]) {
		const withProbe = load((asked) => asked === probe);
		for (const name of Object.keys(table.agents)) {
			if (skillsDirectoryOf(withProbe, name) !== skillsDirectoryOf(table, name)) choosesByDisk.add(name);
		}
	}

	const rows = {};
	for (const [name, agent] of Object.entries(table.agents)) {
		const skillsDirectory = skillsDirectoryOf(table, name);

		// with nothing on disk every path of an "a or b" test is asked for
		world.probes = [];
		world.exists = () => false;
		world.readsFiles = false;
		const installedOnAnEmptyDisk = await agent.detectInstalled(CWD);
		let probes = world.probes.filter((probe) => !probe.startsWith(CWD));
		const readsFiles = world.readsFiles;

		// such an agent keeps only the paths that go with the skills directory this row gives it
		if (choosesByDisk.has(name))
			probes = probes.filter((probe) => skillsDirectoryOf(load((asked) => asked === probe), name) === skillsDirectory);

		// and each of those paths must be enough on its own, or the test is more than "a or b"
		let everyProbeIsEnough = true;
		for (const probe of probes) {
			world.exists = (asked) => asked === probe;
			if (!(await agent.detectInstalled(CWD))) everyProbeIsEnough = false;
		}

		rows[name] = {
			displayName: agent.displayName,
			skillsDirectory: skillsDirectory === undefined ? undefined : tokens(skillsDirectory),
			probes: probes.map(tokens),
			installedOnAnEmptyDisk,
			readsFiles,
			everyProbeIsEnough,
		};
	}
	return { rows, variablesRead };
}

// one platform's rows: each path as its alternatives, the one a set variable gives first and the default last
async function describePlatform(platform) {
	const none = await run(platform, () => false);
	const all = await run(platform, () => true);
	const singles = [];
	for (const variable of [...variables].sort()) singles.push(await run(platform, (name) => name === variable));
	const runs = [all, ...singles, none];

	const described = {};
	for (const [name, row] of Object.entries(none.rows)) {
		const inEveryRun = runs.map((x) => x.rows[name]);
		const result = { displayName: row.displayName };

		if (inEveryRun.some((x) => x.skillsDirectory === undefined))
			result.excluded = "it has no skills directory at user level";
		else if (inEveryRun.some((x) => x.installedOnAnEmptyDisk))
			result.excluded = "its detection does not depend on a path";
		else if (row.probes.length === 0)
			result.excluded = "nothing at user level shows that it is installed";
		else if (inEveryRun.some((x) => x.readsFiles || !x.everyProbeIsEnough || x.probes.length !== row.probes.length))
			result.excluded = "its detection is more than a test for a path";
		else {
			result.skillsDirectory = alternatives(inEveryRun.map((x) => x.skillsDirectory));
			result.detect = row.probes.map((_, index) => alternatives(inEveryRun.map((x) => x.probes[index])));
		}
		described[name] = result;
	}
	return described;
}

// the distinct paths, those that need an environment variable first
function alternatives(paths) {
	const distinct = [...new Set(paths)];
	return [...distinct.filter((x) => x.includes("{env:")), ...distinct.filter((x) => !x.includes("{env:"))];
}

function tokens(value) {
	return value.replaceAll(HOME, "{home}").replace(/\/@@env:([A-Za-z0-9_]+)@@/g, "{env:$1}");
}

function renderDocument() {
	const show = (alternatives) => "`" + alternatives.at(-1).replace("{home}", "~") + "`";
	const lines = [
		"# Supported agents",
		"",
		"<!-- generated by scripts/generate-agents.mjs - do not edit -->",
		"",
		`The ${agents.length} agents \`KaizenTicketing.SkillEmbed\` knows, generated from the agent table of [vercel-labs/skills](${REPOSITORY}) ${source.version} (\`${source.commit.slice(0, 7)}\`).`,
		"",
		"The paths are the defaults on Linux. Where an agent has an environment variable for its directory, such as `CLAUDE_CONFIG_DIR`, the variable wins; some paths differ on macOS and Windows. `src/KaizenTicketing.SkillEmbed/Agents/agents.json` has every alternative.",
		"",
		"| Name | Agent | Skills directory | Installed when one of these exists |",
		"| :--- | :--- | :--- | :--- |",
		...agents.map((x) => `| \`${x.name}\` | ${x.displayName} | ${show(x.skillsDirectory)} | ${x.detect.map(show).join(", ")} |`),
		"",
		"## Agents left out",
		"",
		"These are in the upstream table, and a single test for a path cannot describe them. Add one in your own code with `new SkillAgent(...)`.",
		"",
		"| Name | Agent | Why |",
		"| :--- | :--- | :--- |",
		...excluded.map((x) => `| \`${x.name}\` | ${x.displayName} | ${x.reason[0].toUpperCase()}${x.reason.slice(1)}. |`),
		"",
	];
	return lines.join("\n");
}
