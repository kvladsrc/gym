from hairdeck_model_server.server import HairDeckServer
from model_server_sdk import serve

DEFAULT_PORT = 9133


def main() -> None:
    serve(
        lambda _: HairDeckServer(),
        default_port=DEFAULT_PORT,
        description="HairDeck hair cards from a character image",
    )


if __name__ == "__main__":
    main()
