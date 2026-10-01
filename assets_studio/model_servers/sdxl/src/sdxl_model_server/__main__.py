from model_server_sdk import serve
from sdxl_model_server.server import SdxlServer

DEFAULT_PORT = 9101


def main() -> None:
    serve(lambda _: SdxlServer(), default_port=DEFAULT_PORT, description="SDXL images")


if __name__ == "__main__":
    main()
