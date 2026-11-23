from lato2_model_server.server import Lato2Server
from model_server_sdk import serve

DEFAULT_PORT = 9135


def main() -> None:
    serve(
        lambda _: Lato2Server(),
        default_port=DEFAULT_PORT,
        description="LATO.2 low-poly mesh generation",
    )


if __name__ == "__main__":
    main()
