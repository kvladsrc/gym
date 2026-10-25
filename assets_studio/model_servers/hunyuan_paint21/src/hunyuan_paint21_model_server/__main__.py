from hunyuan_paint21_model_server.server import HunyuanPaint21Server
from model_server_sdk import serve

DEFAULT_PORT = 9120


def main() -> None:
    serve(
        lambda _: HunyuanPaint21Server(),
        default_port=DEFAULT_PORT,
        description="Hunyuan3D-Paint 2.1 (PBR)",
    )


if __name__ == "__main__":
    main()
