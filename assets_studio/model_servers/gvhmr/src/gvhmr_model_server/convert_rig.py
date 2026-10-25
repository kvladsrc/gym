"""Blender entry point: use the shared textured GLB to FBX conversion."""

import runpy
import sys
from pathlib import Path


def main() -> None:
    source, target, processing_path = sys.argv[sys.argv.index("--") + 1 :]
    conversion = runpy.run_path(str(Path(processing_path) / "asset_processing/blender.py"))
    conversion["glb_to_fbx"](Path(source), Path(target))


if __name__ == "__main__":
    main()
