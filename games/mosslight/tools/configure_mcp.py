"""Render or apply this workstation's Blender + project-specific Unity MCP entries."""

import argparse
import json
from pathlib import Path
import re
import tomllib


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    profile = Path.home() / ".nix-profile/bin"
    project = Path(__file__).resolve().parents[1] / "unity"
    entries = {
        "blender": f'command = {json.dumps(str(profile / "blender-mcp"))}\nargs = []\n',
        "unity": f'command = {json.dumps(str(profile / "unity"))}\n'
        f'args = {json.dumps(["mcp", "--project-path", str(project)])}\n',
    }
    path = Path.home() / ".codex/config.toml"
    original = path.read_text()
    updated = original
    for name, values in entries.items():
        block = f"[mcp_servers.{name}]\n{values}startup_timeout_sec = 60\ntool_timeout_sec = 300\n"
        pattern = rf"(?ms)^\[mcp_servers\.{name}\]\n.*?(?=^\[|\Z)"
        if re.search(pattern, updated):
            updated = re.sub(pattern, lambda _: block + "\n", updated, count=1)
        else:
            updated += "\n" + block
        print(block)
    tomllib.loads(updated)
    if args.apply:
        if path.read_text() != original:
            raise RuntimeError("Configuration changed during update")
        path.write_text(updated)
        print("Updated Blender and Unity MCP entries; other configuration preserved.")


if __name__ == "__main__":
    main()
