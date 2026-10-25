from htd_refine_model_server.server import HTDRefineServer
from model_server_sdk import serve


def main() -> None:
    serve(lambda _: HTDRefineServer(), default_port=9127, description="HTD-Refine native motion")


if __name__ == "__main__":
    main()
