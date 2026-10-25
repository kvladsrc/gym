from hunyuan3d21_model_server.server import Hunyuan3D21Server
from model_server_sdk import serve

DEFAULT_PORT = 9119


def main() -> None:
    serve(
        lambda _: Hunyuan3D21Server(),
        default_port=DEFAULT_PORT,
        description="Hunyuan3D-2.1 Shape image to 3D",
    )


if __name__ == "__main__":
    main()
