from model_server_sdk import serve
from prompthmr_model_server.server import PromptHMRServer


def main() -> None:
    serve(
        lambda _: PromptHMRServer(), default_port=9126, description="PromptHMR-video native motion"
    )


if __name__ == "__main__":
    main()
