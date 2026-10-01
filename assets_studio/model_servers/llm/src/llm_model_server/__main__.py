import argparse
import atexit

from llm_model_server.models import DEFAULT_MODEL, MODELS
from llm_model_server.server import LlmServer
from model_server_sdk import serve

DEFAULT_PORT = 9106


def _arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--model", choices=sorted(MODELS), default=DEFAULT_MODEL)


def _make(arguments: argparse.Namespace) -> LlmServer:
    server = LlmServer(MODELS[arguments.model])
    atexit.register(server.close)
    return server


def main() -> None:
    serve(_make, default_port=DEFAULT_PORT, description="Text generation", add_arguments=_arguments)


if __name__ == "__main__":
    main()
