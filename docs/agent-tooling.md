---
title: Local agent tooling
description: Install the repository's skills and role agents for Claude Code and Codex from one Skillfile.
type: guide
---

# Local agent tooling

The [Skillfile](../Skillfile) declares the local sources and project-local
targets. Edit skills under `.skills/` and role contracts under `.agents/`;
installed copies are generated and ignored.

## Install

1. Install Python 3.11 or newer and
   [Skillfile](https://github.com/eljulians/skillfile), version 1.7.1 or newer.
   If Skillfile is missing and you have Rust installed:

    ```sh
    cargo install --locked skillfile
    ```

2. From the repository root, run:

    ```sh
    python3 tools/install-agent-tooling.py
    ```

    This runs `skillfile validate` and `skillfile install --update`, then adapts the
    installed content for Codex. Skills include their `references/` directories.
    No global tool configuration is changed.

3. Start a new Claude Code or Codex session in this checkout. Claude Code reads
   `CLAUDE.md`, which imports `AGENTS.md`. In either client, ask it to use a
   named role, for example: “Use the spec-reviewer agent to review this diff.”
   Models and permissions inherit from your session.

## What is installed

| Content | Claude Code                | Codex                       |
| ------- | -------------------------- | --------------------------- |
| Skills  | `.claude/skills/<name>/`   | `.agents/skills/<name>/`    |
| Agents  | `.claude/agents/<name>.md` | `.codex/agents/<name>.toml` |

Skillfile 1.7.1's built-in Codex adapter writes `.codex/skills/` and skips
agents. The setup command mirrors those skills to Codex's documented discovery
directory and converts the installed Claude role contracts to
[native Codex agents](https://learn.chatgpt.com/docs/agent-configuration/subagents).
Running `skillfile install` alone does not perform that adaptation.

## Refresh and check

After editing a source, or pulling changes, rerun the setup command. It replaces
only the named generated skills and agents, including removed reference files;
unrelated installed entries are left alone. When removing an entire manifest
entry, also delete its generated directories/files from the paths above and
`.codex/skills/`.

```sh
skillfile validate
skillfile status
git status --short
```

Keep `Skillfile` and `Skillfile.lock` in version control. Cache and installations
must not appear in the diff. Local sources are versioned by Git, so Skillfile
leaves their lock empty. In a worktree, run setup separately: installations
belong to the checkout. To remove the setup, delete the generated paths for its
manifest entries; the sources remain available to read directly.

If `skillfile` cannot be found, install it and put its executable on `PATH`.
If a skill or agent does not appear, rerun setup and restart the client. Codex
must support standalone `.codex/agents/*.toml` files with `name`, `description`
and `developer_instructions`; older clients need an upgrade. Resolve Markdown
links against the original `.skills/` or `.agents/` source, as `AGENTS.md`
instructs, since installed copies live at a different depth.
