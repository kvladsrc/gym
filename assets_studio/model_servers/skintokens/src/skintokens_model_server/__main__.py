from model_server_sdk import serve
from skintokens_model_server.server import SkinTokensServer

DEFAULT_PORT = 9116


def main() -> None:
    serve(
        lambda _: SkinTokensServer(),
        default_port=DEFAULT_PORT,
        description="SkinTokens rigging",
    )


if __name__ == "__main__":
    main()
