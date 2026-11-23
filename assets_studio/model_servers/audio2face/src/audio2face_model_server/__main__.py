from audio2face_model_server.server import Audio2FaceServer
from model_server_sdk import serve

DEFAULT_PORT = 9138


def main() -> None:
    serve(
        lambda _: Audio2FaceServer(),
        default_port=DEFAULT_PORT,
        description="Audio2Face-3D facial animation from speech",
    )


if __name__ == "__main__":
    main()
