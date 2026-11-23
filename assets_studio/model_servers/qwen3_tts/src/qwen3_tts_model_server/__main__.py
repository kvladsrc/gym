import argparse

from model_server_sdk import serve
from qwen3_tts_model_server.server import DEFAULT_VARIANT, VARIANTS, Qwen3TtsServer

DEFAULT_PORT = 9139  # design; the studio's recipes run voice on 9140 and clone on 9141


def _arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--model", choices=sorted(VARIANTS), default=DEFAULT_VARIANT)


def _make(arguments: argparse.Namespace) -> Qwen3TtsServer:
    return Qwen3TtsServer(arguments.model)


def main() -> None:
    serve(_make, default_port=DEFAULT_PORT, description="Text to speech", add_arguments=_arguments)


if __name__ == "__main__":
    main()
