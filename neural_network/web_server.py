#!/usr/bin/env python3

import argparse
import json
import math
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

MNIST_IMAGE_SIZE = 28 * 28


class Predictor:
    def __init__(self, executable: Path, model: Path) -> None:
        self._lock = threading.Lock()
        self._process = subprocess.Popen(
            [str(executable), str(model)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            text=True,
            bufsize=1,
        )

    def predict(self, pixels: list[float]) -> list[float]:
        request = " ".join(format(pixel, ".17g") for pixel in pixels)

        with self._lock:
            if self._process.poll() is not None:
                raise RuntimeError("prediction process stopped")
            assert self._process.stdin is not None
            assert self._process.stdout is not None
            self._process.stdin.write(request + "\n")
            self._process.stdin.flush()
            response = self._process.stdout.readline()

        if not response:
            raise RuntimeError("prediction process returned no result")
        probabilities = [float(value) for value in response.split()]
        if len(probabilities) != 10:
            raise RuntimeError("prediction process returned an invalid result")
        return probabilities

    def close(self) -> None:
        if self._process.poll() is None:
            self._process.terminate()
            self._process.wait()


class Server(ThreadingHTTPServer):
    daemon_threads = True


class Handler(BaseHTTPRequestHandler):
    page: bytes
    predictor: Predictor

    def do_GET(self) -> None:
        if self.path != "/":
            self.send_error(404)
            return

        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(self.page)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(self.page)

    def do_POST(self) -> None:
        if self.path != "/predict":
            self.send_error(404)
            return

        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 0 or length > 100_000:
                raise ValueError("invalid request size")
            payload = json.loads(self.rfile.read(length))
            pixels = payload["pixels"]
            if not isinstance(pixels, list) or len(pixels) != MNIST_IMAGE_SIZE:
                raise ValueError("expected 784 grayscale values")
            if any(
                isinstance(pixel, bool)
                or not isinstance(pixel, (int, float))
                or not math.isfinite(pixel)
                or pixel < 0.0
                or pixel > 1.0
                for pixel in pixels
            ):
                raise ValueError("pixels must be numbers between 0 and 1")
        except (KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
            self._send_json(400, {"error": str(error)})
            return

        try:
            probabilities = self.predictor.predict(pixels)
        except (BrokenPipeError, RuntimeError, ValueError) as error:
            self._send_json(500, {"error": str(error)})
            return

        prediction = max(range(len(probabilities)), key=probabilities.__getitem__)
        self._send_json(
            200,
            {
                "prediction": prediction,
                "class_name": str(prediction),
                "probabilities": probabilities,
            },
        )

    def _send_json(self, status: int, payload: dict[str, object]) -> None:
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, message: str, *args: object) -> None:
        print(f"{self.address_string()} - {message % args}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--predictor", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--page", type=Path, required=True)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()

    Handler.page = args.page.read_bytes()
    Handler.predictor = Predictor(args.predictor, args.model)
    server = Server((args.host, args.port), Handler)

    print(f"Open http://{args.host}:{args.port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping server")
    finally:
        server.server_close()
        Handler.predictor.close()


if __name__ == "__main__":
    main()
