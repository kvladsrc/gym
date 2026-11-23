from bpt_model_server.server import BptServer
from model_server_sdk import serve

DEFAULT_PORT = 9137


def main() -> None:
    serve(
        lambda _: BptServer(),
        default_port=DEFAULT_PORT,
        description="BPT artist-like mesh generation",
    )


if __name__ == "__main__":
    main()
