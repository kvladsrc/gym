import os

# Before torch initialises CUDA: activations of many sizes come and go between
# offloaded blocks, and growable segments keep them from fragmenting memory.
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

from flux3_model_server.server import Flux3Server
from model_server_sdk import serve

DEFAULT_PORT = 9149


def main() -> None:
    serve(lambda _: Flux3Server(), default_port=DEFAULT_PORT, description="FLUX 3 Image Klein")


if __name__ == "__main__":
    main()
