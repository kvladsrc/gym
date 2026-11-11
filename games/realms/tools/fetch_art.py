#!/usr/bin/env python3
"""Copy the game's art from Assets Studio into Assets/Realms/Art (not in Git).

Reads art.json: models are copied as Studio made them (at the game's triangle budget, so nothing
is decimated here), except that a model with "texture_size" has its embedded maps resized to it;
icons are resized to 256 px and the tileable ground textures to 1024 px wide with ImageMagick.
Needs the Studio API (default http://127.0.0.1:9000, or $STUDIO_URL). Fetches what is missing or
changed in art.json since the last fetch (Art/manifest.json); --force fetches everything.
"""

import argparse
import json
import os
import pathlib
import shutil
import struct
import subprocess
import urllib.request

here = pathlib.Path(__file__).resolve().parent
game = here.parent
art = game / "unity/realms/Assets/Realms/Art"

TEXTURE_WIDTH = 1024
JSON_CHUNK, BIN_CHUNK = 0x4E4F534A, 0x004E4942


def download(api, asset_id):
    with urllib.request.urlopen(
        f"{api}/api/assets/{asset_id}/file", timeout=60
    ) as reply:
        return reply.read()


def resized(magick, image, geometry):
    """The image (PNG or JPEG bytes) resized by an ImageMagick geometry, as PNG."""
    return subprocess.run(
        [magick, "-", "-resize", geometry, "-strip", "png:-"],
        input=image,
        capture_output=True,
        check=True,
    ).stdout


def shrink_glb(data, size, magick):
    """The GLB with its embedded images no larger than size x size."""
    magic, _, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise ValueError("not a GLB file")
    chunks, offset = {}, 12
    while offset < length:
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        chunks[chunk_type] = data[offset + 8 : offset + 8 + chunk_length]
        offset += 8 + chunk_length
    gltf = json.loads(chunks[JSON_CHUNK])
    old = chunks.get(BIN_CHUNK, b"")
    images = {
        image["bufferView"]: image
        for image in gltf.get("images", [])
        if "bufferView" in image
    }
    if not images:
        return data
    binary = bytearray()
    # Every view moves to its new place; accessors address their views, so they stay as they are.
    for index, view in enumerate(gltf["bufferViews"]):
        start = view.get("byteOffset", 0)
        blob = old[start : start + view["byteLength"]]
        if index in images:
            blob = resized(magick, blob, f"{size}x{size}>")
            images[index]["mimeType"] = "image/png"
        binary += b"\0" * (-len(binary) % 4)
        view["byteOffset"] = len(binary)
        view["byteLength"] = len(blob)
        binary += blob
    binary += b"\0" * (-len(binary) % 4)
    gltf["buffers"][0]["byteLength"] = len(binary)
    text = json.dumps(gltf, separators=(",", ":")).encode()
    text += b" " * (-len(text) % 4)
    total = 12 + 8 + len(text) + 8 + len(binary)
    return b"".join(
        [
            struct.pack("<4sII", b"glTF", 2, total),
            struct.pack("<II", len(text), JSON_CHUNK),
            text,
            struct.pack("<II", len(binary), BIN_CHUNK),
            bytes(binary),
        ]
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--force", action="store_true", help="fetch files that are already there"
    )
    args = parser.parse_args()
    api = os.environ.get("STUDIO_URL", "http://127.0.0.1:9000").rstrip("/")
    spec = json.loads((game / "art.json").read_text())
    magick = shutil.which("magick")
    if not magick:
        raise SystemExit("needs magick on PATH")
    for folder in ("Models", "Icons", "Textures"):
        (art / folder).mkdir(parents=True, exist_ok=True)
    try:
        before = json.loads((art / "manifest.json").read_text())
    except (OSError, ValueError):
        before = {}
    manifest = {"source": api, "models": {}, "icons": {}, "textures": {}}

    def wanted(kind, name, entry, out):
        """Whether to fetch: forced, missing, or changed in art.json since the last fetch."""
        manifest[kind][name] = entry
        return args.force or not out.exists() or before.get(kind, {}).get(name) != entry

    for name, model in spec["models"].items():
        out = art / "Models" / f"{name}.glb"
        if not wanted("models", name, model, out):
            continue
        data = download(api, model["id"])
        if "texture_size" in model:
            data = shrink_glb(data, model["texture_size"], magick)
        out.write_bytes(data)
        print(f"model {name}: {len(data) // 1024} KB")
    for name, asset_id in spec["icons"].items():
        out = art / "Icons" / f"{name}.png"
        if wanted("icons", name, asset_id, out):
            out.write_bytes(resized(magick, download(api, asset_id), "256x256"))
            print(f"icon {name}")
    for name, asset_id in spec.get("textures", {}).items():
        out = art / "Textures" / f"{name}.png"
        if wanted("textures", name, asset_id, out):
            out.write_bytes(
                resized(magick, download(api, asset_id), f"{TEXTURE_WIDTH}x")
            )
            print(f"texture {name}")
    (art / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
