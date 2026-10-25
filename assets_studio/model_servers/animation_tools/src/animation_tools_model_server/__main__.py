from animation_tools_model_server.server import AnimationToolsServer
from model_server_sdk import serve


def main() -> None:
    serve(
        lambda _: AnimationToolsServer(),
        default_port=9124,
        description="Rig preparation and animation transfer (Blender, no neural model)",
    )


if __name__ == "__main__":
    main()
