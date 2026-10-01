import os

# Before torch initialises CUDA: video activations come in many sizes, and
# growable segments keep them from fragmenting the little memory left.
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

from model_server_sdk import serve
from wan_model_server.server import WanServer

DEFAULT_PORT = 9108


def main() -> None:
    serve(lambda _: WanServer(), default_port=DEFAULT_PORT, description="Wan2.2 image to video")


if __name__ == "__main__":
    main()
