from model_server_sdk import serve
from sam3_model_server.server import Sam3Server

DEFAULT_PORT = 9148


def main() -> None:
    serve(
        lambda _: Sam3Server(), default_port=DEFAULT_PORT, description="SAM 3.1 video segmentation"
    )


if __name__ == "__main__":
    main()
