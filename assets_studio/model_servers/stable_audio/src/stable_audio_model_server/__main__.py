from model_server_sdk import serve
from stable_audio_model_server.server import StableAudioServer

DEFAULT_PORT = 9105


def main() -> None:
    serve(
        lambda _: StableAudioServer(),
        default_port=DEFAULT_PORT,
        description="Stable Audio Open sound effects",
    )


if __name__ == "__main__":
    main()
