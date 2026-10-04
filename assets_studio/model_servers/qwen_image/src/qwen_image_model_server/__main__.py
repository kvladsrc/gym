import os

# Before torch initialises CUDA: activations of many sizes come and go between
# offloaded blocks, and growable segments keep them from fragmenting memory.
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

from model_server_sdk import serve
from qwen_image_model_server.server import QwenImageServer

DEFAULT_PORT = 9109


def main() -> None:
    serve(
        lambda _: QwenImageServer(), default_port=DEFAULT_PORT, description="Qwen-Image 2.1 images"
    )


if __name__ == "__main__":
    main()
