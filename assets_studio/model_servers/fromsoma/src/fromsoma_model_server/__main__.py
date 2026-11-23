from fromsoma_model_server.server import FromSomaServer
from model_server_sdk import serve

DEFAULT_PORT = 9142


def main() -> None:
    serve(
        lambda _: FromSomaServer(),
        default_port=DEFAULT_PORT,
        description="fromsoma: a person on SOMA's mesh and skeleton from two bald-head pictures",
    )


if __name__ == "__main__":
    main()
