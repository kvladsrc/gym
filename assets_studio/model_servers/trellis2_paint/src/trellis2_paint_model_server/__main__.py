from model_server_sdk import serve
from trellis2_paint_model_server.server import TrellisPaintServer


def main() -> None:
    serve(lambda _: TrellisPaintServer(), default_port=9131, description="TRELLIS.2 Paint")


if __name__ == "__main__":
    main()
