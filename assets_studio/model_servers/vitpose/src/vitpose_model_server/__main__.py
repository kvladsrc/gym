from model_server_sdk import serve
from vitpose_model_server.server import VitPoseServer


def main() -> None:
    serve(
        lambda _: VitPoseServer(),
        default_port=9134,
        description="ViTPose++ 2D joints of a character in a video",
    )


if __name__ == "__main__":
    main()
