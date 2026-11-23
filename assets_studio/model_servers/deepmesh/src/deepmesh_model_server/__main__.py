from deepmesh_model_server.server import DeepMeshServer
from model_server_sdk import serve

DEFAULT_PORT = 9136


def main() -> None:
    serve(
        lambda _: DeepMeshServer(),
        default_port=DEFAULT_PORT,
        description="DeepMesh artist-like mesh generation",
    )


if __name__ == "__main__":
    main()
