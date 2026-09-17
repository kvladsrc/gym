"""Small stdio MCP client, using the configured Blender server verbatim."""

import argparse
import json
import os
from pathlib import Path
import select
import subprocess
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", choices=["blender", "unity"], default="blender")
    parser.add_argument("tool")
    parser.add_argument("arguments", nargs="?", default="{}", help="JSON object")
    args = parser.parse_args()
    launcher = Path(__file__).with_name("run.sh")
    command = ["bash", str(launcher), "blender-mcp"]
    if args.server == "unity":
        project = launcher.parent.parent / "unity"
        command = ["bash", str(launcher), "cli", "mcp", "--project-path", str(project)]
    proc = subprocess.Popen(command,
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    pending = b""

    def send(message):
        proc.stdin.write(json.dumps(message).encode() + b"\n")
        proc.stdin.flush()

    def request(number, method, params):
        nonlocal pending
        send({"jsonrpc": "2.0", "id": number, "method": method, "params": params})
        deadline = time.monotonic() + 180
        while time.monotonic() < deadline:
            if b"\n" not in pending:
                ready, _, _ = select.select([proc.stdout], [], [], 1)
                if not ready:
                    continue
                data = os.read(proc.stdout.fileno(), 65536)
                if not data:
                    raise RuntimeError("MCP process exited")
                pending += data
                continue
            line, pending = pending.split(b"\n", 1)
            message = json.loads(line)
            if message.get("id") == number:
                if "error" in message:
                    raise RuntimeError(message["error"])
                return message["result"]
        raise TimeoutError(method)

    try:
        request(1, "initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                                  "clientInfo": {"name": "mosslight", "version": "1"}})
        send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        if args.tool == "list":
            result = request(2, "tools/list", {})
        else:
            result = request(2, "tools/call", {"name": args.tool,
                                              "arguments": json.loads(args.arguments)})
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if result.get("isError"):
            raise RuntimeError("MCP tool failed")
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()


if __name__ == "__main__":
    main()
