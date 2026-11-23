from model_server_sdk import serve
from pbrfusion4_model_server.server import PBRFusion4Server

DEFAULT_PORT = 9145


def main() -> None:
    serve(
        lambda _: PBRFusion4Server(),
        default_port=DEFAULT_PORT,
        description="PBRFusion 4 height and normal maps",
    )


if __name__ == "__main__":
    main()
