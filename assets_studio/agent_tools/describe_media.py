"""Ask Studio's local Qwen to inspect media; stdout contains text, never pixels."""

import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

DEFAULT_PROMPT = (
    "Кратко опиши видимое. Укажи число людей, видимость головы, кистей и стоп, "
    "перекрытия и обрезанные части тела. Для анимации отметь видимые проблемы позы. "
    "Не додумывай закрытые детали и не делай выводов о 3D-риге по одному изображению."
)


class StudioClient:
    def __init__(self, url: str) -> None:
        self.url = url.rstrip("/")

    def call(self, path: str, data: Any = None) -> Any:
        request = urllib.request.Request(  # noqa: S310 -- explicitly selected Studio API
            self.url + path,
            data=None if data is None else json.dumps(data).encode(),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(request, timeout=60) as response:  # noqa: S310
            return json.load(response)

    def upload(self, path: Path) -> str:
        query = urllib.parse.urlencode({"title": path.name})
        request = urllib.request.Request(  # noqa: S310 -- same local Studio
            self.url + "/api/assets?" + query,
            path.read_bytes(),
            headers={"Content-Type": "application/octet-stream"},
        )
        with urllib.request.urlopen(request, timeout=120) as response:  # noqa: S310
            return str(json.load(response)["id"])

    def text(self, asset: str) -> str:
        with urllib.request.urlopen(  # noqa: S310 -- asset result, text only
            self.url + "/api/assets/" + asset + "/file", timeout=60
        ) as response:
            return response.read().decode("utf-8-sig")


def fingerprint(path: Path) -> str:
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def describe(args: argparse.Namespace) -> dict[str, Any]:
    client = StudioClient(args.studio)
    servers = client.call("/api/servers")
    server = next((s for s in servers if s["id"] == args.server), None)
    if server is None:
        raise RuntimeError(f"Studio has no server {args.server}")
    suffix = Path(args.path).suffix.lower() if args.path else ""
    task = "video-to-text" if args.video or suffix in {".mp4", ".webm"} else "image-to-text"
    # Do not silently switch to a remote vision API or generate repeated waiting jobs.
    if task not in {t["task"] for t in server.get("tasks", [])}:
        raise RuntimeError(f"{args.server} has not advertised {task}; start its model server once")
    if args.reference and task != "image-to-text":
        raise ValueError("--reference is only supported for images")
    paths = (
        {"video" if task == "video-to-text" else "image": Path(args.path).expanduser()}
        if args.path
        else {}
    )
    if args.reference:
        paths["reference"] = Path(args.reference).expanduser()
    params = {"max_tokens": args.max_tokens, "thinking": False, "temperature": 0.0}
    if task == "video-to-text":
        params["video_frames"] = args.frames
    signature = {
        "version": 1,
        "studio": args.studio,
        "server": args.server,
        "model": server.get("model"),
        "task": task,
        "prompt": args.prompt,
        "params": params,
        "files": {role: fingerprint(p) for role, p in paths.items()},
        "asset": args.asset,
    }
    key = hashlib.sha256(json.dumps(signature, sort_keys=True).encode()).hexdigest()
    cache = (
        Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache"))
        / "assets-studio/media-reports"
    )
    cache.mkdir(parents=True, exist_ok=True)
    target = cache / (key + ".json")
    if target.exists() and not args.refresh:
        return json.loads(target.read_text())
    inputs = {role: client.upload(path) for role, path in paths.items()}
    if args.asset:
        inputs["video" if task == "video-to-text" else "image"] = args.asset
    request = {
        "server": args.server,
        "task": task,
        "prompt": args.prompt,
        "params": params,
        "inputs": inputs,
        "count": 1,
        "seed": 0,
    }
    if not args.refresh:
        request["idempotency_key"] = "local-media-" + key
    job = client.call("/api/jobs", request)
    deadline = time.monotonic() + args.timeout
    while job["status"] not in {"succeeded", "failed", "cancelled"}:
        if time.monotonic() >= deadline:
            raise TimeoutError(
                f"Job {job['id']} is still {job['status']}; inspect it before resubmitting"
            )
        time.sleep(2)
        job = client.call("/api/jobs/" + job["id"])
    if job["status"] != "succeeded":
        raise RuntimeError(f"Job {job['id']}: {job.get('error_message') or job['status']}")
    asset = job["outputs"][0]
    info = client.call("/api/assets/" + asset)
    report = {
        "job_id": job["id"],
        "asset_id": asset,
        "request": signature,
        "meta": info.get("meta", {}),
        "timing": job.get("timing"),
        "text": client.text(asset),
    }
    temporary = target.with_suffix(f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    temporary.replace(target)
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--path", help="Local image or short video")
    source.add_argument("--asset", help="Existing Studio asset ID; use --video for video assets")
    parser.add_argument("--reference", help="Optional second local image for comparison")
    parser.add_argument("--video", action="store_true")
    parser.add_argument("--server", default="qwen")
    parser.add_argument("--studio", default="http://127.0.0.1:9000")
    parser.add_argument("--prompt", default=DEFAULT_PROMPT)
    parser.add_argument("--frames", type=int, choices=range(2, 7), default=4)
    parser.add_argument("--max-tokens", type=int, default=512, choices=range(32, 2049))
    parser.add_argument("--timeout", type=float, default=1800)
    parser.add_argument(
        "--refresh", action="store_true", help="Explicitly recompute an existing report"
    )
    parser.add_argument(
        "--output", type=Path, help="Save the complete report with provenance as JSON"
    )
    args = parser.parse_args()
    try:
        report = describe(args)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        print(report["text"])
        print(f"Studio job: {report['job_id']}; text asset: {report['asset_id']}", file=sys.stderr)
        if report["meta"].get("finish_reason") == "length":
            print("Report reached its token limit; conclusions may be incomplete.", file=sys.stderr)
    except (OSError, ValueError, RuntimeError, urllib.error.URLError) as error:
        parser.exit(1, f"Local media analysis failed: {error}\n")


if __name__ == "__main__":
    main()
