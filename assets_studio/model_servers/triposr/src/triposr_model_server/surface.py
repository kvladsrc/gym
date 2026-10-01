"""CPU surface extraction in place of the compiled ``torchmcubes`` package.

TripoSR imports ``torchmcubes.marching_cubes``, a C++/CUDA extension that
needs the CUDA toolkit to build. This module provides the same function via
scikit-image (Lorensen marching cubes on the CPU) and is registered under
the ``torchmcubes`` name before TripoSR is imported, so the upstream source
stays unmodified. Neural inference still runs on the GPU.

scikit-image returns array-axis coordinates (z, y, x); torchmcubes returns
(x, y, z). Geometry may differ from torchmcubes in ambiguous cells.
"""

import sys
import types

import numpy as np
import torch
from skimage.measure import marching_cubes as _extract


def marching_cubes(volume: torch.Tensor, threshold: float) -> tuple[torch.Tensor, torch.Tensor]:
    vertices, faces, _, _ = _extract(
        volume.detach().float().cpu().numpy(),
        level=threshold,
        method="lorensen",
        gradient_direction="ascent",
        allow_degenerate=False,
    )
    return (
        torch.from_numpy(vertices[:, [2, 1, 0]].copy()).to(volume.device),
        torch.from_numpy(faces.astype(np.int64).copy()).to(volume.device),
    )


def install() -> None:
    """Make ``import torchmcubes`` resolve to this module's implementation."""
    module = types.ModuleType("torchmcubes")
    module.marching_cubes = marching_cubes  # type: ignore[attr-defined]
    sys.modules["torchmcubes"] = module
