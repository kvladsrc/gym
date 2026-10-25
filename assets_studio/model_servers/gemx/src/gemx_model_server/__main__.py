from gemx_model_server.server import GEMXServer
from model_server_sdk import serve

DEFAULT_PORT = 9129


def main() -> None:
    serve(
        lambda _: GEMXServer(),
        default_port=DEFAULT_PORT,
        description="GEM-X whole-body motion capture (SOMA)",
    )


if __name__ == "__main__":
    main()
