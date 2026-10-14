#!/usr/bin/env python3

import json
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys


def check_destination(path, root):
    current = root
    for part in path.relative_to(root).parts:
        current /= part
        if current.is_symlink():
            raise ValueError(f"Refusing to overwrite a symlink: {current}")


def install(root):
    if shutil.which("skillfile") is None:
        raise ValueError("Install skillfile first; see docs/agent-tooling.md.")

    entries = []
    targets = set()
    for line in (root / "Skillfile").read_text(encoding="utf-8").splitlines():
        fields = shlex.split(line, comments=True)
        if not fields:
            continue
        if fields[0] == "install":
            if fields not in (["install", "claude-code", "local"], ["install", "codex", "local"]):
                raise ValueError(f"Expected a project-local Claude Code or Codex target: {line}")
            targets.add(fields[1])
            continue
        if fields[0] != "local" or len(fields) not in (3, 4):
            raise ValueError(f"Only local sources and built-in targets are supported: {line}")
        entity_type, source_path = fields[1], fields[-1]
        name = fields[2] if len(fields) == 4 else Path(source_path).stem
        if not re.fullmatch(r"[a-zA-Z0-9_-]+", name):
            raise ValueError(f"Invalid generated entry name: {name}")
        if entity_type not in ("skill", "agent"):
            raise ValueError(f"Unsupported entry type: {entity_type}")
        entries.append((entity_type, name, source_path))

    if targets != {"claude-code", "codex"}:
        raise ValueError("The Skillfile must declare both project-local targets.")
    subprocess.run(["skillfile", "validate"], cwd=root, check=True)
    subprocess.run(["skillfile", "install", "--update"], cwd=root, check=True)

    for entity_type, name, source_path in entries:
        if entity_type == "skill":
            source = root / ".codex/skills" / name
            destination = root / ".agents/skills" / name
            check_destination(destination, root)
            if destination.exists():
                shutil.rmtree(destination)
            shutil.copytree(source, destination)
        elif entity_type == "agent":
            installed = root / ".claude/agents" / f"{name}.md"
            text = installed.read_text(encoding="utf-8")
            frontmatter, separator, body = text.removeprefix("---\n").partition("\n---\n")
            if not text.startswith("---\n") or not separator:
                raise ValueError(f"Missing agent frontmatter: {installed}")
            metadata = dict(line.split(": ", 1) for line in frontmatter.splitlines())
            if metadata.get("name") != name or not metadata.get("description"):
                raise ValueError(f"Expected matching name and one-line description: {installed}")

            instructions = (
                f"Follow this role contract, whose canonical source is {source_path}. "
                "Resolve its Markdown links relative to that canonical source in the repository root.\n\n"
                + body.strip()
            )
            values = {
                "name": name,
                "description": metadata["description"],
                "developer_instructions": instructions,
            }
            destination = root / ".codex/agents" / f"{name}.toml"
            check_destination(destination, root)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(
                "".join(f"{key} = {json.dumps(value, ensure_ascii=False)}\n" for key, value in values.items()),
                encoding="utf-8",
            )
    print("Installed repository skills and agents for Claude Code and Codex.")


if __name__ == "__main__":
    try:
        install(Path(__file__).resolve().parents[1])
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        sys.exit(str(error))
