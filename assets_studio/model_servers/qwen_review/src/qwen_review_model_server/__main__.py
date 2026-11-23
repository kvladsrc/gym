import argparse

from model_server_sdk import serve
from qwen_review_model_server.server import BASES, DEFAULT_BASE, QwenReviewServer

DEFAULT_PORT = 9146


def _add_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--gpu-layers",
        type=int,
        default=None,
        metavar="N",
        help="keep at most N decoder layers on the GPU (default: as many as fit);"
        " the others are copied there for each pass",
    )
    parser.add_argument(
        "--base",
        choices=sorted(BASES),
        default=DEFAULT_BASE,
        help=f"the base model the adapter was trained on (default: {DEFAULT_BASE})",
    )


def main() -> None:
    serve(
        lambda arguments: QwenReviewServer(gpu_layers=arguments.gpu_layers, base=arguments.base),
        default_port=DEFAULT_PORT,
        description="Qwen3.5 with a LoRA: a game screenshot to its predicted Steam reviews",
        add_arguments=_add_arguments,
    )


if __name__ == "__main__":
    main()
