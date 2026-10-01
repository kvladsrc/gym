"""Print the runtime environment and check surface extraction on the device."""

import json
import subprocess

import torch
import trimesh

from triposr_model_server import surface
from triposr_model_server.server import SOURCE_REVISION, source_directory


def main() -> None:
    source = source_directory().expanduser()
    revision = subprocess.run(  # noqa: S603 (fixed git command on a local path)
        ["git", "-C", str(source), "rev-parse", "HEAD"],  # noqa: S607
        capture_output=True,
        text=True,
        check=False,
    ).stdout.strip()
    device = "cuda" if torch.cuda.is_available() else "cpu"
    volume = torch.zeros(8, 9, 10, device=device)
    volume[2:6, 2:7, 2:8] = 1
    vertices, faces = surface.marching_cubes(volume, 0.5)
    shape = trimesh.Trimesh(
        vertices=vertices[:, [2, 1, 0]].cpu().numpy(), faces=faces.cpu().numpy()
    )
    report = {
        "torch": torch.__version__,
        "cuda": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "source": str(source),
        "source_revision_ok": revision == SOURCE_REVISION,
        "surface_on_device": vertices.device.type == device,
        "surface_watertight": bool(shape.is_watertight and shape.volume > 0),
    }
    print(json.dumps(report, indent=2))
    if not (report["source_revision_ok"] and report["surface_watertight"]):
        raise SystemExit("environment is not ready; run `just setup`")


if __name__ == "__main__":
    main()
