from model_server_sdk import serve
from unirig_model_server.server import UniRigServer

DEFAULT_PORT = 9117


def main() -> None:
    serve(
        lambda _: UniRigServer(),
        default_port=DEFAULT_PORT,
        description="UniRig rigging",
    )


if __name__ == "__main__":
    main()
