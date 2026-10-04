import os

# Before torch is imported: the paint pipeline is offloaded block by block.
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

from hunyuan_paint_model_server.server import HunyuanPaintServer
from model_server_sdk import serve

DEFAULT_PORT = 9114


def main() -> None:
    serve(
        lambda _: HunyuanPaintServer(),
        default_port=DEFAULT_PORT,
        description="Hunyuan3D-Paint: a mesh painted from its image",
    )


if __name__ == "__main__":
    main()
