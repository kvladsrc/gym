from hi3d_model_server.server import Hi3DServer
from model_server_sdk import serve

DEFAULT_PORT = 9130


def main() -> None:
    serve(
        lambda _: Hi3DServer(),
        default_port=DEFAULT_PORT,
        description="Hi3D v1.0 mesh refinement",
    )


if __name__ == "__main__":
    main()
