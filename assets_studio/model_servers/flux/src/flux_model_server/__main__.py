from flux_model_server.server import FluxServer
from model_server_sdk import serve

DEFAULT_PORT = 9104


def main() -> None:
    serve(lambda _: FluxServer(), default_port=DEFAULT_PORT, description="FLUX.1 schnell images")


if __name__ == "__main__":
    main()
