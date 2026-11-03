from model_server_sdk import serve
from unlimited_ocr_model_server.server import UnlimitedOCRServer

DEFAULT_PORT = 9132


def main() -> None:
    serve(
        lambda _: UnlimitedOCRServer(),
        default_port=DEFAULT_PORT,
        description="Unlimited-OCR 2: image to text",
    )


if __name__ == "__main__":
    main()
