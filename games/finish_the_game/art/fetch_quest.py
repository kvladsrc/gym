"""Brings the demo quest's finished studio jobs (art/quest.json) into the
Unity project, Assets/FinishTheGame/Art/Quest: the lines to Voice/<id>.wav,
the sounds to Sound/<id>.wav, a rigged character to
Art/Characters/<name>.fbx (and art/characters.json); the images' variants to
a review folder (the chosen ones go on to 3D with art/studio.py, a model into
Unity with art/convert_models.py).

    python3 art/fetch_quest.py [--review DIR] [--studio http://127.0.0.1:9000]
"""

import argparse
import json
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
ART = HERE.parent / "unity" / "Finish the game" / "Assets" / "FinishTheGame" / "Art"
QUEST = ART / "Quest"


def get(url: str) -> bytes:
    return urllib.request.urlopen(url).read()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--studio", default="http://127.0.0.1:9000")
    parser.add_argument("--review", type=Path, help="where to put the images' variants")
    args = parser.parse_args()
    jobs = json.loads((HERE / "quest.json").read_text())
    waiting = []
    for name, job_id in jobs.items():
        job = json.loads(get(f"{args.studio}/api/jobs/{job_id}"))
        if job["status"] != "succeeded":
            waiting.append(f"{name}: {job['status']}")
            continue
        kind, what = name.split(":", 1)
        outputs = job["outputs"]
        if kind in ("line", "sound"):
            folder = QUEST / ("Voice" if kind == "line" else "Sound")
            folder.mkdir(parents=True, exist_ok=True)
            (folder / f"{what}.wav").write_bytes(
                get(f"{args.studio}/api/assets/{outputs[0]}/file")
            )
        elif kind == "rig":
            (ART / "Characters" / f"{what}.fbx").write_bytes(
                get(f"{args.studio}/api/assets/{outputs[0]}/file")
            )
            characters = json.loads((HERE / "characters.json").read_text())
            characters[what] = outputs[0]
            (HERE / "characters.json").write_text(
                json.dumps(characters, indent=2) + "\n"
            )
        elif kind == "image" and args.review:
            args.review.mkdir(parents=True, exist_ok=True)
            for i, asset in enumerate(outputs):
                (args.review / f"{what}_{i}.png").write_bytes(
                    get(f"{args.studio}/api/assets/{asset}/file")
                )
    print("\n".join(waiting) or "all fetched")


if __name__ == "__main__":
    main()
