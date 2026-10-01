from model_server_sdk import serve
from triposr_model_server.server import TripoSRServer

DEFAULT_PORT = 9102


def main() -> None:
    serve(lambda _: TripoSRServer(), default_port=DEFAULT_PORT, description="TripoSR image-to-3D")


if __name__ == "__main__":
    main()
