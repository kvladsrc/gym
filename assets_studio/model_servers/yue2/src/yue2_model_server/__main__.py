from model_server_sdk import serve
from yue2_model_server.server import YuE2Server

DEFAULT_PORT = 9110


def main() -> None:
    serve(lambda _: YuE2Server(), default_port=DEFAULT_PORT, description="YuE2 songs")


if __name__ == "__main__":
    main()
