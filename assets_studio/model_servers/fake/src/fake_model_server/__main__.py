import argparse

from fake_model_server.server import FakeModelServer
from model_server_sdk import serve

DEFAULT_PORT = 9100


def _add_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--model-id", default="fake", help="report this model id")
    parser.add_argument(
        "--task", action="append", dest="tasks", help="declare only this task (repeatable)"
    )
    parser.add_argument("--load-delay", type=float, default=0.0, help="seconds to 'load' the model")
    parser.add_argument("--load-error", help="fail loading with this message")


def _make_server(arguments: argparse.Namespace) -> FakeModelServer:
    return FakeModelServer(
        model_id=arguments.model_id,
        tasks=arguments.tasks,
        load_delay_s=arguments.load_delay,
        load_error=arguments.load_error,
    )


def main() -> None:
    serve(
        _make_server,
        default_port=DEFAULT_PORT,
        description="Deterministic GPU-free model server for testing the studio.",
        add_arguments=_add_arguments,
    )


if __name__ == "__main__":
    main()
