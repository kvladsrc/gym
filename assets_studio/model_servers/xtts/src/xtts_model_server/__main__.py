from model_server_sdk import serve
from xtts_model_server.server import XttsServer

DEFAULT_PORT = 9103


def main() -> None:
    serve(lambda _: XttsServer(), default_port=DEFAULT_PORT, description="XTTS-v2 speech")


if __name__ == "__main__":
    main()
