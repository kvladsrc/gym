from hunyuan3d_model_server.server import Hunyuan3DServer
from model_server_sdk import serve

DEFAULT_PORT = 9112


def main() -> None:
    serve(
        lambda _: Hunyuan3DServer(),
        default_port=DEFAULT_PORT,
        description="Hunyuan3D-2mini image to 3D",
    )


if __name__ == "__main__":
    main()
