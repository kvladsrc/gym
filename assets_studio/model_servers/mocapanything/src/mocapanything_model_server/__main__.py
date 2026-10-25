from mocapanything_model_server.server import MoCapAnythingServer
from model_server_sdk import serve

DEFAULT_PORT = 9122


def main() -> None:
    serve(
        lambda _: MoCapAnythingServer(),
        default_port=DEFAULT_PORT,
        description="MoCapAnything V2 motion capture",
    )


if __name__ == "__main__":
    main()
