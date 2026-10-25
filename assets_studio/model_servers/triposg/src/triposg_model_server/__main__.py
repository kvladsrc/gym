from model_server_sdk import serve
from triposg_model_server.server import TripoSGServer

DEFAULT_PORT = 9115


def main() -> None:
    serve(
        lambda _: TripoSGServer(),
        default_port=DEFAULT_PORT,
        description="TripoSG image to 3D",
    )


if __name__ == "__main__":
    main()
