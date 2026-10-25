from kimodo_model_server.server import VARIANTS, KimodoServer
from model_server_sdk import serve

DEFAULT_PORT = 9118


def main() -> None:
    serve(
        lambda arguments: KimodoServer(arguments.model),
        default_port=DEFAULT_PORT,
        description="Kimodo text to motion",
        add_arguments=lambda parser: parser.add_argument(
            "--model",
            choices=sorted(VARIANTS),
            default="soma",
            help="soma: FBX on Mixamo names (default); smplx: native SMPL-X body motion",
        ),
    )


if __name__ == "__main__":
    main()
