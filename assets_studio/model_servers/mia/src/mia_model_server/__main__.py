from mia_model_server.server import MiaServer
from model_server_sdk import serve

DEFAULT_PORT = 9113


def main() -> None:
    serve(
        lambda _: MiaServer(), default_port=DEFAULT_PORT, description="Make-It-Animatable rigging"
    )


if __name__ == "__main__":
    main()
