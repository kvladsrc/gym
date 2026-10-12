"""Breadth-first search over asset combinations through the studio's REST API.

A stand-in outside the studio's design (like the launcher): a spec (TOML)
lists axes (categories with prompt templates and subjects, styles, genres,
image models, 3D models); every combination becomes a chain of jobs
(text → image → 3D), and every result is tagged with its axes
(``category:hero``, ``style:cartoon``, ``genre:sci-fi``, ``image:flux``,
``3d:hunyuan3d``, ``sweep:<name>``) to be filtered and rated in the studio
(ADR-007).

    uv run python sweep/sweep.py queue SPEC.toml   # queue the jobs
    uv run python sweep/sweep.py tag SPEC.toml     # tag finished results (repeatable)
    uv run python sweep/sweep.py status SPEC.toml
    uv run python sweep/sweep.py paint SPEC.toml   # paint shape-only meshes from their images

A category with ``images`` (asset ids of concepts already in the studio)
instead of subjects remakes 3D from those images: no text-to-image job, no
style or genre (a mesh made again after a change to a 3D server).

The manifest (job ids per combination) is kept in
~/.local/share/assets-studio/sweeps/<name>.json, so ``tag`` can be run
again as jobs finish.
"""

import argparse
import itertools
import json
import os
import random
import sys
import tomllib
import urllib.error
import urllib.request
from collections import Counter
from pathlib import Path
from typing import Any

STUDIO = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000")
SWEEPS = (
    Path(os.environ.get("XDG_DATA_HOME", Path.home() / ".local/share")) / "assets-studio" / "sweeps"
)


def call(method: str, path: str, body: Any = None) -> Any:
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(  # noqa: S310 -- the studio's http URL
        f"{STUDIO}{path}", data, {"Content-Type": "application/json"}, method=method
    )
    with urllib.request.urlopen(request, timeout=30) as response:  # noqa: S310
        return json.load(response)


def combinations(spec: dict[str, Any]) -> list[dict[str, Any]]:
    """Every category × style; a genre and a subject per combination, drawn
    with the spec's seed (``genres_per`` genres each), or every subject with
    ``every_subject = true`` (a set of assets rather than a search)."""
    rng = random.Random(spec.get("seed", 1))  # noqa: S311 -- reproducible draws, not secrets
    genres = list(spec["genres"])
    out = []
    texts = {name: c for name, c in spec["categories"].items() if "images" not in c}
    for (category, c), style in itertools.product(texts.items(), spec["styles"]):
        for genre in rng.sample(genres, k=min(spec.get("genres_per", 1), len(genres))):
            subjects = c["subjects"] if spec.get("every_subject") else [rng.choice(c["subjects"])]
            for subject in subjects:
                prompt = c["template"].format(
                    subject=subject, style=spec["styles"][style], genre=spec["genres"][genre]
                )
                out.append(
                    {
                        "category": category,
                        "style": style,
                        "genre": genre,
                        "subject": subject,
                        "prompt": prompt,
                    }
                )
    return out


def mesh_jobs(spec: dict[str, Any], category: str, image: dict[str, Any]) -> list[dict[str, str]]:
    """The 3D jobs of one image: ``image`` is the job's input, either
    ``{"dependencies": {...}}`` (an image job of the sweep) or ``{"inputs": {...}}``
    (an image already in the studio)."""
    meshes = []
    c = spec["categories"][category]
    for model in c.get("3d", spec["3d_models"]):
        mesh = spec["3d_models"][model]
        params = {**mesh.get("params", {}), **c.get("3d_params", {}).get(model, {})}
        job = call(
            "POST",
            "/api/jobs",
            {"server": mesh["server"], "task": "image-to-3d", "params": params, **image},
        )
        meshes.append({"model": model, "job": job["id"]})
    return meshes


def queue(spec: dict[str, Any], manifest: Path) -> None:
    if manifest.exists():
        sys.exit(f"{manifest} exists: this sweep was queued already")
    entries = []
    for category, c in spec["categories"].items():
        for asset in c.get("images", []):
            entry = {
                "category": category,
                "style": "existing",
                "genre": "none",
                "subject": asset,
                "prompt": None,
                "image_model": "existing",
                "image_job": None,
                "image_asset": asset,
            }
            entry["meshes"] = mesh_jobs(spec, category, {"inputs": {"image": asset}})
            entries.append(entry)
    for combo in combinations(spec):
        category = spec["categories"][combo["category"]]
        for image_model, image in spec["image_models"].items():
            job = call(
                "POST",
                "/api/jobs",
                {
                    "server": image["server"],
                    "task": "text-to-image",
                    "prompt": combo["prompt"],
                    "count": 1,
                    # A category can change the image (a panorama's size).
                    "params": {**image.get("params", {}), **category.get("image_params", {})},
                },
            )
            entry = {**combo, "image_model": image_model, "image_job": job["id"]}
            entry["meshes"] = mesh_jobs(
                spec,
                combo["category"],
                {"dependencies": {"image": {"job_id": job["id"], "output_index": 0}}},
            )
            entries.append(entry)
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps({"name": spec["name"], "entries": entries}, indent=1))
    meshes = sum(len(e["meshes"]) for e in entries)
    print(f"queued {len(entries)} images and {meshes} meshes; manifest {manifest}")


def tags_of(name: str, entry: dict[str, Any], model: str | None) -> list[str]:
    tags = [
        f"sweep:{name}",
        f"category:{entry['category']}",
        f"style:{entry['style']}",
        f"genre:{entry['genre']}",
        f"image:{entry['image_model']}",
    ]
    return [*tags, f"3d:{model}"] if model else [*tags, "stage:concept"]


def tag(manifest: Path) -> None:
    data = json.loads(manifest.read_text())
    done = Counter()
    for entry in data["entries"]:
        targets = [(entry["image_job"], None)] if entry["image_job"] else []
        targets += [(m["job"], m["model"]) for m in entry["meshes"]]
        for job_id, model in targets:
            job = call("GET", f"/api/jobs/{job_id}")
            done[job["status"]] += 1
            for asset in job["outputs"]:
                wanted = tags_of(data["name"], entry, model)
                current = call("GET", f"/api/assets/{asset}")
                if sorted(set(current["tags"]) | set(wanted)) != current["tags"]:
                    call(
                        "PATCH",
                        f"/api/assets/{asset}",
                        {"tags": sorted(set(current["tags"]) | set(wanted))},
                    )
    print(dict(done))


# Reconstructions without colour, painted by the paint server (3d-paint).
SHAPE_ONLY = ("hunyuan3d",)


def paint(manifest: Path) -> None:
    """Queue 3d-paint for every shape-only mesh of the sweep, with the image it
    was made from; recorded as the mesh model "<model>+paint"."""
    data = json.loads(manifest.read_text())
    # Paint jobs already in the studio, by the mesh job they paint: a rerun
    # (or a run that stopped halfway) adds none twice.
    existing = {
        job["dependencies"]["mesh"]["job_id"]: job["id"]
        for job in call("GET", "/api/jobs?limit=1000")
        if job["task"] == "3d-paint" and "mesh" in job["dependencies"]
    }
    queued = 0
    for entry in data["entries"]:
        have = {m["model"] for m in entry["meshes"]}
        for mesh in list(entry["meshes"]):
            painted = f"{mesh['model']}+paint"
            if mesh["model"] not in SHAPE_ONLY or painted in have:
                continue
            if mesh["job"] in existing:
                entry["meshes"].append({"model": painted, "job": existing[mesh["job"]]})
                continue
            if call("GET", f"/api/jobs/{mesh['job']}")["status"] in ("failed", "cancelled"):
                continue
            dependencies = {"mesh": {"job_id": mesh["job"], "output_index": 0}}
            if entry["image_job"]:
                dependencies["image"] = {"job_id": entry["image_job"], "output_index": 0}
            job = call(
                "POST",
                "/api/jobs",
                {
                    "server": "paint",
                    "task": "3d-paint",
                    "params": {},
                    "dependencies": dependencies,
                    # An image already in the studio: an input, not a job's output.
                    "inputs": {"image": entry["image_asset"]} if not entry["image_job"] else {},
                },
            )
            entry["meshes"].append({"model": painted, "job": job["id"]})
            queued += 1
            manifest.write_text(json.dumps(data, indent=1))  # after every job
    manifest.write_text(json.dumps(data, indent=1))
    print(f"queued {queued} paint jobs")


def status(manifest: Path) -> None:
    data = json.loads(manifest.read_text())
    counts: Counter[str] = Counter()
    for entry in data["entries"]:
        for job_id in [entry["image_job"], *(m["job"] for m in entry["meshes"])]:
            if job_id is None:
                continue
            counts[call("GET", f"/api/jobs/{job_id}")["status"]] += 1
    print(dict(counts))


def main() -> None:
    parser = argparse.ArgumentParser(description="Queue and tag combinations of asset axes.")
    parser.add_argument("command", choices=["queue", "tag", "status", "paint"])
    parser.add_argument("spec", type=Path)
    args = parser.parse_args()
    spec = tomllib.loads(args.spec.read_text())
    manifest = SWEEPS / f"{spec['name']}.json"
    try:
        {
            "queue": lambda: queue(spec, manifest),
            "tag": lambda: tag(manifest),
            "status": lambda: status(manifest),
            "paint": lambda: paint(manifest),
        }[args.command]()
    except urllib.error.HTTPError as error:
        sys.exit(f"the studio refused: HTTP {error.code}: {error.read().decode()[:300]}")
    except urllib.error.URLError as error:
        sys.exit(f"studio unreachable at {STUDIO}: {error}")


if __name__ == "__main__":
    main()
