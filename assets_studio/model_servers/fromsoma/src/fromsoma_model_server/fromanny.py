"""Model server for fromanny: a realistic person from Anny's parameters and two pictures of a bald
head, in Anny's own face space.

It works like fromsoma, with one difference: the reader reads Anny's own face modifiers instead
of MHR's head variation. It was trained from DINOv2's weights on renders of random Anny people
whose faces came from Anny's modifiers alone, with no MHR in the data or the weights. Like
fromsoma's reader, it is told the parameters. It reads the head's modifiers (cheeks, chin, eyes,
brows, mouth, nose, ears, the skull's shape and the forehead; each left and right pair as one
value; not the three that move the whole head against the neck) and the 14 pigment sliders. The
values are clamped to Anny's -1..1. A child keeps Anny's child head, with the modifiers read on
it. The body, the rig and the pictures' colour (texture=pictures, facetex.py) are fromsoma's.

The server shares fromsoma's root directory (FROMSOMA_ROOT) with its SOMA-X data and Face
Landmarker; its reader is fromanny.pt there.
"""

from pathlib import Path
from typing import Any

from fromsoma_model_server import server
from fromsoma_model_server.server import FromSomaServer
from model_server_sdk import ModelInfo, serve

MODEL_FILE = "fromanny.pt"
FORMAT = "fromanny.reader.v1"
DEFAULT_PORT = 9144


def missing_files(root: Path) -> list[str]:
    return server.missing_files(root, MODEL_FILE)


class FromAnnyServer(FromSomaServer):
    model = ModelInfo(
        id="fromanny",
        name="fromanny face reader",
        license=(
            "reader weights: trained locally (README); SOMA-X, Anny, DINOv2 and MediaPipe Face"
            " Landmarker: Apache-2.0; MPFB2 assets: CC0"
        ),
        source="https://github.com/naver/anny",
        details={
            "network": "DINOv2 ViT-S/14 on front and profile, an eye-strip CNN, known sex and age",
            "reads": (
                "Anny's face modifiers of the head (left and right as one) and 14 pigment"
                " sliders, given the Anny values"
            ),
            "body": "Anny's body of the parameters, as fromsoma's",
            "texture": (
                "the pigments painted; on the head, the pictures' own colour projected through"
                " cameras fitted to the head (texture=pictures)"
            ),
            "output": (
                "SOMA mesh, root + 77 joints, 4 weights per vertex, eyeballs, one 4096 texture;"
                " metres, Y up, face toward +Z"
            ),
            "device": "CPU",
        },
    )
    model_file = MODEL_FILE
    model_format = FORMAT

    def face_meta(self, coefficients: list[float]) -> dict[str, Any]:
        """Anny's face modifiers by name, as clamped for the body."""
        names = self._bodies.modifiers
        return {
            "anny_face": {
                name: round(min(1.0, max(-1.0, v)), 4)
                for name, v in zip(names, coefficients, strict=True)
            }
        }


def main() -> None:
    serve(
        lambda _: FromAnnyServer(),
        default_port=DEFAULT_PORT,
        description="fromanny: a person on SOMA's mesh and skeleton from two bald-head pictures",
    )


if __name__ == "__main__":
    main()
