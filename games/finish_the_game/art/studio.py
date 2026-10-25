"""Small helpers over the asset studio's REST API for the game's art:
queue a model's shape and paint from a reviewed concept, rate an asset, and
keep the chosen chains in art/chains.json.

    python3 art/studio.py model <name> <concept asset> <max faces> [--rating N]
    python3 art/studio.py rate <asset> <rating> [tag ...]
    python3 art/studio.py outputs <job>
"""

import json
import sys
import urllib.request
from pathlib import Path

API = "http://127.0.0.1:9000/api"
CHAINS = Path(__file__).resolve().parent / "chains.json"


def call(path: str, body: dict | None = None, method: str = "GET") -> dict:
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(
        API + path,
        data=data,
        method=method if data is None else (method if method != "GET" else "POST"),
        headers={"Content-Type": "application/json"},
    )
    return json.load(urllib.request.urlopen(request))


def rate(asset: str, rating: int, tags: list[str]) -> None:
    call(
        f"/assets/{asset}",
        {"rating": rating, "tags": ["finish_the_game", *tags]},
        "PATCH",
    )


def model(name: str, concept: str, faces: int, rig: bool = False) -> dict:
    shape = call(
        "/jobs",
        {
            "server": "hunyuan3d",
            "task": "image-to-3d",
            "inputs": {"image": concept},
            "params": {
                "steps": 50,
                "guidance_scale": 5.0,
                "octree_resolution": 384,
                "max_faces": faces,
            },
        },
    )
    paint = call(
        "/jobs",
        {
            "server": "paint",
            "task": "3d-paint",
            "inputs": {"image": concept},
            "params": {"texture_size": 2048, "max_faces": faces},
            "dependencies": {"mesh": {"job_id": shape["id"], "output_index": 0}},
        },
    )
    chains = json.loads(CHAINS.read_text()) if CHAINS.exists() else {}
    chains[name] = {"concept": concept, "shape": shape["id"], "paint": paint["id"]}
    if rig:
        # A character: the painted mesh rigged to Mixamo's skeleton, from its A-pose.
        rigged = call(
            "/jobs",
            {
                "server": "rig",
                "task": "3d-to-rig",
                "inputs": {},
                "params": {"rest_pose": "t-pose", "input_pose": "a-pose"},
                "dependencies": {"mesh": {"job_id": paint["id"], "output_index": 0}},
            },
        )
        chains[name]["rig"] = rigged["id"]
    CHAINS.write_text(json.dumps(chains, indent=2) + "\n")
    return chains[name]


if __name__ == "__main__":
    command, *rest = sys.argv[1:]
    if command == "model":
        print(
            model(
                rest[0], rest[1], int(rest[2]), rig=len(rest) > 3 and rest[3] == "rig"
            )
        )
    elif command == "rate":
        rate(rest[0], int(rest[1]), rest[2:])
    elif command == "outputs":
        print(" ".join(call(f"/jobs/{rest[0]}").get("outputs") or []))
