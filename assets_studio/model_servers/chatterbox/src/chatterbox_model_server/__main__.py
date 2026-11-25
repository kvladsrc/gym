from chatterbox_model_server.server import ChatterboxServer
from model_server_sdk import serve

DEFAULT_PORT = 9147


def main() -> None:
    serve(lambda _: ChatterboxServer(), default_port=DEFAULT_PORT, description="Chatterbox speech")


if __name__ == "__main__":
    main()
