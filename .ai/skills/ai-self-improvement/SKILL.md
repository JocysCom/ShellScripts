---
name: ai-self-improvement
description: Update, create, improve, and synchronise this repository's AI agent instructions and related assets (including skills). Use when the user asks to create or edit a skill/SKILL.md, modify the agent's own instructions/processes, restructure instruction governance, migrate instruction content into skills, or run/adjust the sync pipeline that publishes `.ai/` sources into agent-specific folders. Load this skill before writing any SKILL.md, .instructions.md, or touching any skills/ folder (.ai/, .claude/, .roo/, .github/). It tells you the correct location (.ai/) and the sync step, so files end up in the right place.
---

# AI Self-Improvement (Instructions + Skills + Agents)

## Critical: `.ai/` is the Primary Source for ALL Agents

The `.ai/` folder is the **single source of truth** for all AI agent configurations in this repository. Agent definitions are stored in `agents.json` and the sync script propagates changes automatically.

### What gets synced

| Asset | Source | Scope | Description |
|-------|--------|-------|-------------|
| Instructions | `.ai/*instructions.md` | Project | Agent rules and guidelines |
| Skills | `.ai/skills/` | Project | Reusable skill definitions |
| Custom agents | `.ai/agents/` | Project | Agent prompt templates for this repository |
| Global agents | `.ai/.global/agents/` | User | Agent prompt templates for ALL repositories (synced with `-Global` flag) |

### Agent configuration (`agents.json`)

The file `agents.json` (next to this SKILL.md) defines each agent's sync targets:

| Agent | Instructions | Skills | Project Agents | Global Agents |
|-------|-------------|--------|----------------|---------------|
| **Cline** | `.clinerules/` (multiple) | — | — | — |
| **Roo Code** | `.roo/rules/` (multiple) | `.roo/skills/` | `.roomodes` (JSON) | `{AppData}/.../custom_modes.yaml` (JSON) |
| **GitHub Copilot** | `.github/copilot-instructions.md` (single) | `.github/skills/` | `.github/agents/` | — |
| **OpenAI Codex** | `AGENTS.md` (single) | — | — | — |
| **Claude Code** | `.claude/` (multiple) | `.claude/skills/` | `.claude/commands/` | `~/.claude/commands/` |

**IMPORTANT:** When asked to modify skills, instructions, or custom agents, you MUST:

1. Locate the source file under `.ai/` (not the agent-specific output)
2. Make changes to the `.ai/` source
3. Run the sync script to propagate changes to all agents

### Custom agents format

Source files in `.ai/agents/` use YAML frontmatter + markdown body. The frontmatter captures metadata for all platforms, and the sync script transforms to each platform's native format:

- **GitHub Copilot** — synced directly to `.github/agents/` (native Copilot agent format)
- **Claude Code** — synced to `.claude/commands/` (Claude uses the full file as a slash command)
- **Roo Code** — transformed into `.roomodes` JSON (maps `name` → `name`, `description` → `roleDefinition`, body → `customInstructions`, `groups` → `groups`)

```yaml
---
name: Repository Analyze and Sync
description: Regenerate the architecture map and sync changes to all agents.
tools: ["search", "edit", "runCommands"]
groups: ["read", "edit", "command"]
---

Prompt instructions here...
```

| Field | Copilot | Claude | Roo Code |
|-------|---------|--------|----------|
| `name` | Agent display name | — | Mode display name |
| `description` | Agent description | — | `roleDefinition` |
| `tools` | Tool access list | — | — |
| `groups` | — | — | Permission groups (`read`, `edit`, `command`, `mcp`) |
| Body | Agent instructions | Slash command prompt | `customInstructions` |

## Path Mapping Reference

When you encounter a path in an agent-specific folder, map it to `.ai/`:

| Agent-Specific Path | Source Path (Edit Here) |
|---------------------|------------------------|
| `.roo/rules/*.md` | `.ai/*.instructions.md` |
| `.roo/skills/<name>/SKILL.md` | `.ai/skills/<name>/SKILL.md` |
| `.github/copilot-instructions.md` | `.ai/instructions.md` (generated) |
| `.github/agents/<name>.md` | `.ai/agents/<name>.md` |
| `AGENTS.md` | `.ai/instructions.md` (generated) |
| `.claude/*.instructions.md` | `.ai/*.instructions.md` |
| `.claude/skills/<name>/SKILL.md` | `.ai/skills/<name>/SKILL.md` |
| `.claude/commands/<name>.md` | `.ai/agents/<name>.md` |

**Example:** If asked to update `.roo/skills/ai-self-improvement/SKILL.md`, you must edit `.ai/skills/ai-self-improvement/SKILL.md` instead.

## Editable files (sources of truth)

- `.ai/instructions.md` — the main system instructions file
- `.ai/*instructions.md` — additional instruction files (auto-included)
- `.ai/*instructions-detail.md` — detailed instruction files (read only when needed)
- `.ai/skills/<name>/SKILL.md` — skill definition files
- `.ai/agents/<name>.md` — project-level agent prompt templates
- `.ai/.global/agents/<name>.md` — global agent prompt templates (all repositories)

## Workflow

1. Treat `.ai/` as the **single source of truth** for agent instructions, skills, and custom agents.
2. When creating or migrating a skill, create/update it under `.ai/skills/`.
3. When creating a custom agent, create it under `.ai/agents/` using YAML frontmatter + markdown.
4. Make instruction changes in `.ai/instructions.md` and related `*.instructions.md` files.
5. Do **not** edit generated outputs directly (they are produced by the sync script):
   - `.roo/rules/`, `.roo/skills/`
   - `.github/copilot-instructions.md`, `.github/skills/`, `.github/agents/`
   - `AGENTS.md`
   - `.claude/*.instructions.md`, `.claude/skills/`, `.claude/commands/`
6. **Test changes before syncing** — verify scripts execute correctly and changes work as expected.
7. After testing, run the sync script to apply to all agents.

## Testing Before Sync

Before running the sync script, always verify your changes work correctly:

- **For script changes**: Execute the modified script and verify output is correct
- **For instruction changes**: Review the markdown renders properly and instructions are clear
- **For skill changes**: Test any bundled tools or scripts included in the skill

## Activation process

After editing instruction files, skills, or custom agents, run from repository root:

```powershell
# Project-level sync (default — safe)
.\.ai\skills\ai-self-improvement\scripts\Sync-AgentAssets.ps1 AUTO

# Include global agents (affects ALL repositories — use carefully)
.\.ai\skills\ai-self-improvement\scripts\Sync-AgentAssets.ps1 AUTO -Global
```

### Sync modes

| Mode | Description |
|------|-------------|
| `AUTO` | Update only agents detected in this repository (default) |
| `ALL` | Update all known agent outputs |
| Agent name | Update a specific agent (e.g. `"Claude Code"`, `roo-code`) |
| `-Global` | Also sync `.ai/.global/agents/` to user-level paths (off by default) |

## Single source of truth

**Never embed template content in instructions — reference template files instead.**

- "Template maintained in `pr/checklist.template.md`"
- Do not paste template content into instructions

## Bundled files

- Agent config: `.ai/skills/ai-self-improvement/agents.json`
- Sync script: `.ai/skills/ai-self-improvement/scripts/Sync-AgentAssets.ps1`
