"""Cheap repository checks; no Unity, Blender or third-party Python packages."""

import ast
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    for path in (ROOT / "tools").glob("*.py"):
        ast.parse(path.read_text(), filename=str(path))
    for path in (ROOT / "unity/Packages").glob("*.json"):
        json.loads(path.read_text())
    version = (ROOT / "unity/ProjectSettings/ProjectVersion.txt").read_text()
    if "m_EditorVersion: 6000.6.0f1" not in version:
        raise ValueError("Review and update the pinned Editor version explicitly")
    guids = {}
    for meta in (ROOT / "unity/Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        if match is None:
            raise ValueError(f"Missing GUID: {meta}")
        guid = match.group(1)
        if guid in guids:
            raise ValueError(f"Duplicate asset GUID: {meta} and {guids[guid]}")
        guids[guid] = meta
    for asset in (ROOT / "unity/Assets").rglob("*"):
        if asset.suffix in {".cs", ".unity", ".mat"} and not Path(str(asset) + ".meta").exists():
            raise ValueError(f"Missing .meta: {asset}")
    # Text-only repository policy: binary art and local output must stay ignored.
    repo = ROOT.parents[1]
    tracked = subprocess.check_output(
        ["git", "ls-files", "--", "games/mosslight"], cwd=repo, text=True
    ).splitlines()
    forbidden = {".blend", ".fbx", ".png", ".mp4", ".wav", ".x86_64"}
    for name in tracked:
        if Path(name).suffix.lower() in forbidden or "/Library/" in name or "/artifacts/" in name:
            raise ValueError(f"Local artifact unexpectedly tracked: {name}")
    print(f"Mosslight source checks passed; {len(guids)} unique asset GUIDs")


if __name__ == "__main__":
    main()
