"""Mesh previews (ADR-011): pictures the web UI draws for thumbnails, kept as WebP
apart from the assets' files and dropped with their asset."""

import io
from pathlib import Path

import httpx2 as httpx
import pytest
from PIL import Image
from studio.services.library import PreviewError
from test_api import studio_server
from test_studio import png, running_studio

from model_server_sdk import media


def glb() -> bytes:
    return media.encode_glb(*media.tetrahedron())


def picture(size: tuple[int, int] = (64, 48), image_format: str = "PNG") -> bytes:
    """Opaque red on the left half, transparent on the right."""
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    image.paste((200, 30, 30, 255), (0, 0, size[0] // 2, size[1]))
    buffer = io.BytesIO()
    (image.convert("RGB") if image_format == "JPEG" else image).save(buffer, format=image_format)
    return buffer.getvalue()


def put_preview(http: httpx.Client, asset_id: str, data: bytes, version: int = 1) -> httpx.Response:
    return http.put(
        f"/api/assets/{asset_id}/preview",
        params={"version": version},
        content=data,
        headers={"Content-Type": "application/octet-stream"},
    )


def test_a_mesh_preview_is_kept_as_webp_and_listed(tmp_path: Path) -> None:
    with studio_server(tmp_path) as url, httpx.Client(base_url=url, timeout=30) as http:
        mesh = http.post("/api/assets", content=glb()).json()
        assert mesh["preview_url"] is None
        assert mesh["preview_version"] is None

        stored = put_preview(http, mesh["id"], picture())
        assert stored.status_code == 200, stored.text
        preview_url = stored.json()["preview_url"]
        assert preview_url.startswith(f"/api/assets/{mesh['id']}/preview?v=")
        assert stored.json()["preview_version"] == 1
        assert http.get(f"/api/assets/{mesh['id']}").json()["preview_url"] == preview_url
        (listed,) = http.get("/api/assets").json()
        assert listed["preview_url"] == preview_url

        served = http.get(preview_url)
        assert served.status_code == 200
        assert served.headers["content-type"] == "image/webp"
        assert "immutable" in served.headers["cache-control"]
        image = Image.open(io.BytesIO(served.content))
        assert image.format == "WEBP"
        assert image.size == (64, 48)
        alpha = image.convert("RGBA").getchannel("A")
        assert (alpha.getpixel((5, 5)), alpha.getpixel((60, 5))) == (255, 0)
        # Without the version of the picture, the browser must ask again next time.
        unversioned = http.get(f"/api/assets/{mesh['id']}/preview")
        assert unversioned.headers["cache-control"] == "no-cache"

        webp = put_preview(http, mesh["id"], picture(image_format="WEBP"), version=2)
        assert webp.status_code == 200, webp.text
        assert webp.json()["preview_version"] == 2


def test_only_a_mesh_takes_a_usable_picture(tmp_path: Path) -> None:
    with studio_server(tmp_path) as url, httpx.Client(base_url=url, timeout=30) as http:
        mesh = http.post("/api/assets", content=glb()).json()
        image = http.post("/api/assets", content=png()).json()
        assert http.get(f"/api/assets/{mesh['id']}/preview").status_code == 404
        assert put_preview(http, "missing", picture()).status_code == 404

        refused = {
            "only a mesh": put_preview(http, image["id"], picture()),
            "PNG or WebP": put_preview(http, mesh["id"], picture(image_format="JPEG")),
            "at most 1024": put_preview(http, mesh["id"], picture((1025, 8))),
            "PNG or WebP ": put_preview(http, mesh["id"], b"not a picture"),
        }
        for reason, response in refused.items():
            assert response.status_code == 422, reason
            assert reason.strip() in response.json()["detail"]
        no_version = http.put(f"/api/assets/{mesh['id']}/preview", content=picture())
        assert no_version.status_code == 422
        assert http.get(f"/api/assets/{mesh['id']}").json()["preview_url"] is None


def test_deleting_a_mesh_removes_its_preview(tmp_path: Path) -> None:
    with studio_server(tmp_path) as url, httpx.Client(base_url=url, timeout=30) as http:
        mesh = http.post("/api/assets", content=glb()).json()
        preview_url = put_preview(http, mesh["id"], picture()).json()["preview_url"]
        assert len(list((tmp_path / "previews").rglob("*.webp"))) == 1

        deleted = http.delete(f"/api/assets/{mesh['id']}").json()
        assert deleted["preview_url"] is None
        assert http.get(preview_url).status_code == 410
        assert put_preview(http, mesh["id"], picture()).status_code == 410
        assert list((tmp_path / "previews").rglob("*.webp")) == []


def test_a_new_preview_replaces_the_old_file_unless_shared(tmp_path: Path) -> None:
    with running_studio(tmp_path) as studio:
        library = studio.library
        first = library.import_bytes(glb())
        twin = library.import_bytes(glb())  # same content: the same picture, one file
        red, blue = picture(), picture((32, 32))

        first = library.set_preview(first, red, 1)
        twin = library.set_preview(twin, red, 1)
        assert first is not None
        assert twin is not None
        shared = library.preview_file(first)
        assert shared is not None
        assert shared == library.preview_file(twin)

        first = library.set_preview(first, blue, 2)
        assert first is not None
        assert first.preview_version == 2
        assert shared.exists()  # the twin still shows it
        assert library.preview_file(first) != shared

        library.delete(twin.id)
        assert not shared.exists()
        own = library.preview_file(first)
        assert own is not None
        assert own.exists()
        library.delete(first.id)
        assert not own.exists()

        with pytest.raises(PreviewError, match="only a mesh"):
            library.set_preview(library.import_bytes(png()), red, 1)
