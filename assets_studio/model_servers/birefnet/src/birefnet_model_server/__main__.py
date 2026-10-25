from birefnet_model_server.server import BiRefNetServer
from model_server_sdk import serve

DEFAULT_PORT = 9121


def main() -> None:
    serve(
        lambda _: BiRefNetServer(),
        default_port=DEFAULT_PORT,
        description="BiRefNet background removal",
    )


if __name__ == "__main__":
    main()
