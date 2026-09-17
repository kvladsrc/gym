"""Queue a build in the manually opened Editor and wait for its own result."""

import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]


def call(tool, arguments):
    completed = subprocess.run(
        [sys.executable, str(ROOT / "tools/mcp_call.py"), "--server", "unity",
         tool, json.dumps(arguments)],
        capture_output=True, text=True, timeout=190,
    )
    if completed.returncode:
        raise RuntimeError(completed.stdout + completed.stderr)
    response = json.loads(completed.stdout)
    for block in response.get("content", []):
        if block.get("type") == "text":
            value = json.loads(block["text"])
            if isinstance(value, dict):
                if value.get("success") is False:
                    raise RuntimeError(value)
                return value
    raise RuntimeError(f"Missing JSON result: {response}")


def main():
    queued = call("build", {
        "target": "StandaloneLinux64",
        "outputPath": "../artifacts/build/Mosslight.x86_64",
        "scenes": ["Assets/Scenes/Mosslight.unity"],
        "options": ["Development"], "confirm": True,
    })
    if queued.get("status") != "queued" or not queued.get("buildId"):
        raise RuntimeError(queued)
    build_id = queued["buildId"]
    print(f"Queued {build_id}", flush=True)
    deadline = time.monotonic() + 600
    while time.monotonic() < deadline:
        status = call("build_status", {})
        if status.get("buildId") != build_id:
            raise RuntimeError(f"Unexpected build: {status.get('buildId')}")
        if status.get("status") == "completed":
            report = ROOT / "artifacts/build-report.json"
            report.parent.mkdir(parents=True, exist_ok=True)
            report.write_text(json.dumps(status, indent=2) + "\n")
            print(f"Build {status.get('result')}; report: {report}")
            if status.get("result") != "Succeeded":
                raise RuntimeError(status.get("errors", status))
            return
        print(f"Build {status.get('status')}", flush=True)
        time.sleep(3)
    raise TimeoutError("Build still pending; inspect build_status before retrying")


if __name__ == "__main__":
    main()
