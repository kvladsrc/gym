from model_server_sdk import serve
from sam2_model_server.server import Sam2Server


def main() -> None:
    serve(lambda _: Sam2Server(), default_port=9125, description="SAM 2.1 subject tracking")


if __name__ == "__main__":
    main()
