from gvhmr_model_server.server import GVHMRServer
from model_server_sdk import serve

DEFAULT_PORT = 9123


def main() -> None:
    serve(
        lambda _: GVHMRServer(), default_port=DEFAULT_PORT, description="GVHMR human motion capture"
    )


if __name__ == "__main__":
    main()
