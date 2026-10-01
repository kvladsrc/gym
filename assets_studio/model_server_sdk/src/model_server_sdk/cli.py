"""Command-line entry point shared by model servers."""

import argparse
import logging
import os
from collections.abc import Callable, Sequence

import uvicorn

from model_server_sdk.server import ModelServer, create_app


def serve(
    make_server: Callable[[argparse.Namespace], ModelServer],
    *,
    default_port: int,
    description: str,
    add_arguments: Callable[[argparse.ArgumentParser], None] | None = None,
    argv: Sequence[str] | None = None,
) -> None:
    """Parse ``--host``/``--port`` (plus server-specific options) and serve.

    ``MODEL_SERVER_HOST`` and ``MODEL_SERVER_PORT`` override the defaults.
    The server listens on loopback unless told otherwise.
    """
    parser = argparse.ArgumentParser(description=description)
    parser.add_argument("--host", default=os.environ.get("MODEL_SERVER_HOST", "127.0.0.1"))
    parser.add_argument(
        "--port", type=int, default=int(os.environ.get("MODEL_SERVER_PORT", default_port))
    )
    parser.add_argument("--log-level", default="info")
    if add_arguments is not None:
        add_arguments(parser)
    arguments = parser.parse_args(argv)
    logging.basicConfig(
        level=arguments.log_level.upper(), format="%(levelname)s %(name)s: %(message)s"
    )
    app = create_app(make_server(arguments))
    uvicorn.run(app, host=arguments.host, port=arguments.port, log_level=arguments.log_level)
