"""One painting job, in its own process, in the Hunyuan3D-2.1 source
directory (the server runs it). upstream's textureGenPipeline, with two of
its models swapped for ones that fit 8 GB:

- the multiview diffusion model (upstream's multiviewDiffusionNet, loaded
  the same way) with only the UNet and VAE kept on the GPU (fit_on_gpu),
  and DINOv2-giant (2.2 GB) in RAM, on the GPU only for its single call;
- RealESRGAN x4plus through spandrel instead of the realesrgan package.

    python -m hunyuan_paint21_model_server.paint WORK_DIR PARAMS_JSON

WORK_DIR holds input.glb and image; textured.glb is written there. The last
line of output is ``RESULT {json}``.
"""

import io
import json
import os
import sys

# Before torch: lets the allocator grow segments instead of fragmenting.
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")
from pathlib import Path
from typing import Any

import numpy as np
from asset_processing.blender import exit_now
from PIL import Image

from hunyuan_paint21_model_server.server import (
    RESULT_PREFIX,
    Params,
    dino_directory,
    paint_directory,
    realesrgan_path,
)

BACKGROUND_MODEL = "isnet-general-use"  # as the other 3D servers: keeps thin parts
FAINT_ALPHA = 32
TRANSPARENT_BELOW = 128


def prepare_image(data: bytes, remove_background: bool) -> Image.Image:
    image = Image.open(io.BytesIO(data))
    image.load()
    alpha = image.mode in ("RGBA", "LA", "PA")
    cut = alpha and image.getchannel("A").getextrema()[0] < TRANSPARENT_BELOW
    if remove_background and not cut:
        import rembg

        image = rembg.remove(image.convert("RGB"), session=rembg.new_session(BACKGROUND_MODEL))
    rgba = image.convert("RGBA")
    rgba.putalpha(rgba.getchannel("A").point(lambda a: 0 if a < FAINT_ALPHA else a))
    return rgba


def on_gpu_while_called(module: Any) -> Any:
    """A module that sits in RAM and visits the GPU for each call."""
    import torch

    def call(*args: Any, **kwargs: Any) -> Any:
        module.to("cuda")
        try:
            return module(*args, **kwargs)
        finally:
            module.to("cpu")
            torch.cuda.empty_cache()

    return call


def fit_on_gpu(pipeline: Any) -> None:
    """The UNet and the VAE on the GPU; the rest in RAM.

    diffusers' model offload does not fit this pipeline: it puts its inputs
    on self.vae.device before the offload hook has moved the VAE. So the VAE
    stays on the GPU; the CLIP text encoder (used once a job) visits it
    through an accelerate hook, offloaded when the UNet first runs; the
    pipeline's image encoder is never used (DINOv2 replaces it) and stays in
    RAM. The UNet's feed-forward layers run in chunks of 16 tokens (where
    12 views at once ran out of memory; the result is the same)."""
    from accelerate import cpu_offload_with_hook

    pipeline.unet.to("cuda")
    pipeline.vae.to("cuda")
    pipeline.enable_vae_slicing()
    _, text_hook = cpu_offload_with_hook(pipeline.text_encoder, "cuda")
    cpu_offload_with_hook(pipeline.unet, "cuda", prev_module_hook=text_hook)
    for module in pipeline.unet.modules():
        if type(module).__name__ == "BasicTransformerBlock":
            module.set_chunk_feed_forward(16, 1)  # divides 8x8 (512) and 12x12 (768) levels


def swap_models(pipeline_module: Any, multiview_module: Any) -> None:
    import torch
    from diffusers import DiffusionPipeline, UniPCMultistepScheduler
    from omegaconf import OmegaConf

    class OffloadedMultiview(multiview_module.multiviewDiffusionNet):
        def __init__(self, config: Any) -> None:  # upstream's, from local weights, offloaded
            self.device = config.device
            self.cfg = OmegaConf.load(config.multiview_cfg_path)
            self.mode = self.cfg.model.params.stable_diffusion_config.custom_pipeline[2:]
            here = os.path.dirname(multiview_module.__file__)
            custom = os.path.join(here, "..", "hunyuanpaintpbr")
            pipeline = DiffusionPipeline.from_pretrained(
                str(paint_directory()), custom_pipeline=custom, torch_dtype=torch.float16
            )
            pipeline.scheduler = UniPCMultistepScheduler.from_config(
                pipeline.scheduler.config, timestep_spacing="trailing"
            )
            pipeline.set_progress_bar_config(disable=True)
            pipeline.eval()
            pipeline.view_size = self.cfg.model.params.get("view_size", 320)
            fit_on_gpu(pipeline)
            self.pipeline = pipeline
            if getattr(pipeline.unet, "use_dino", False):
                from hunyuanpaintpbr.unet.modules import (  # pyright: ignore[reportMissingImports]
                    Dino_v2,
                )

                self.dino_v2 = on_gpu_while_called(Dino_v2(config.dino_ckpt_path).to(torch.float16))

        def __call__(self, *args: Any, **kwargs: Any) -> Any:
            """The views, then the pipeline out of the GPU: it is not needed
            again, and baking at twice the texture size needs the room."""
            try:
                return super().__call__(*args, **kwargs)
            finally:
                for module in (self.pipeline.unet, self.pipeline.vae, self.pipeline.text_encoder):
                    module.to("cpu")
                torch.cuda.empty_cache()

    class SpandrelSuper:
        def __init__(self, config: Any) -> None:
            from spandrel import ModelLoader

            self.model = ModelLoader().load_from_file(config.realesrgan_ckpt_path).model
            self.model = self.model.half().eval().to("cuda")  # ~70 MB

        @torch.no_grad()
        def __call__(self, image: Image.Image) -> Image.Image:
            x = torch.from_numpy(np.array(image.convert("RGB"))).permute(2, 0, 1)
            x = x[None].half().div(255).to("cuda")
            y = self.model(x).clamp(0, 1)[0].permute(1, 2, 0).float().cpu().numpy()
            return Image.fromarray((y * 255).round().astype(np.uint8))

    pipeline_module.multiviewDiffusionNet = OffloadedMultiview
    pipeline_module.imageSuperNet = SpandrelSuper


def plain_pbr(path: Path) -> None:
    """Drop the specular extension Blender's glTF export adds: it reads the OBJ's Ks = 1 as a
    doubled specular (KHR_materials_specular, factor 2), which made every model glossy; Paint
    2.1's material is base colour + metallic + roughness only."""
    import struct

    data = path.read_bytes()
    length = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20 : 20 + length])
    for material in gltf.get("materials", []):
        material.get("extensions", {}).pop("KHR_materials_specular", None)
        if not material.get("extensions", True):
            material.pop("extensions")
    for key in ("extensionsUsed", "extensionsRequired"):
        if key in gltf:
            gltf[key] = [e for e in gltf[key] if e != "KHR_materials_specular"]
            if not gltf[key]:
                gltf.pop(key)
    chunk = json.dumps(gltf, separators=(",", ":")).encode()
    chunk += b" " * (-len(chunk) % 4)
    rest = data[20 + length :]
    header = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(chunk) + len(rest))
    path.write_bytes(header + struct.pack("<II", len(chunk), 0x4E4F534A) + chunk + rest)


def read_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    """The JSON and the binary chunk of a GLB."""
    import struct

    data = path.read_bytes()
    length = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20 : 20 + length])
    rest = data[20 + length :]
    binary = rest[8 : 8 + struct.unpack("<I", rest[:4])[0]] if rest else b""
    return gltf, binary


def write_glb(path: Path, gltf: dict[str, Any], binary: bytes) -> None:
    import struct

    chunk = json.dumps(gltf, separators=(",", ":")).encode()
    chunk += b" " * (-len(chunk) % 4)
    binary += b"\0" * (-len(binary) % 4)
    body = struct.pack("<II", len(chunk), 0x4E4F534A) + chunk
    if binary:
        body += struct.pack("<II", len(binary), 0x004E4942) + binary
    path.write_bytes(struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body)


def floor_roughness(path: Path, minimum: float, max_metallic: float = 1.0) -> None:
    """Roughness (the G channel of each metallic-roughness texture, glTF) raised
    to at least ``minimum``, metalness (B) lowered to at most ``max_metallic``.
    Paint 2.1 reads gloss off the image: wet-looking skin, bone and painted
    highlights came out at 0.3, mirror-like under the viewer's environment
    light. The image is re-encoded as PNG and the binary chunk rebuilt, each
    buffer view 4-byte aligned."""
    if minimum <= 0 and max_metallic >= 1:
        return
    gltf, binary = read_glb(path)
    images = {
        gltf["textures"][pbr["metallicRoughnessTexture"]["index"]]["source"]
        for material in gltf.get("materials", [])
        if "metallicRoughnessTexture" in (pbr := material.get("pbrMetallicRoughness", {}))
    }
    views = {
        gltf["images"][i]["bufferView"]: i for i in images if "bufferView" in gltf["images"][i]
    }
    if not views:
        return
    floor, ceiling = round(minimum * 255), round(max_metallic * 255)
    rebuilt = bytearray()
    for index, view in enumerate(gltf["bufferViews"]):
        start = view.get("byteOffset", 0)
        data = binary[start : start + view["byteLength"]]
        if index in views:
            pixels = np.array(Image.open(io.BytesIO(data)).convert("RGB"))
            pixels[..., 1] = np.maximum(pixels[..., 1], floor)
            pixels[..., 2] = np.minimum(pixels[..., 2], ceiling)
            buffer = io.BytesIO()
            Image.fromarray(pixels).save(buffer, format="PNG")
            data = buffer.getvalue()
            gltf["images"][views[index]]["mimeType"] = "image/png"
        rebuilt += b"\0" * (-len(rebuilt) % 4)
        view["byteOffset"] = len(rebuilt)
        view["byteLength"] = len(data)
        rebuilt += data
    gltf["buffers"][0]["byteLength"] = len(rebuilt)
    write_glb(path, gltf, bytes(rebuilt))


def simplify(source: Path, target: Path, max_faces: int) -> None:
    """upstream's mesh_simplify_trimesh, for today's trimesh: it passes the
    triangle count as the first argument of simplify_quadric_decimation,
    which is now a fraction to remove (fast_simplification rejects it)."""
    import trimesh

    mesh = trimesh.load(source, force="mesh")
    if len(mesh.faces) > max_faces:
        mesh = mesh.simplify_quadric_decimation(face_count=max_faces)
    mesh.export(target)


def paint(work: Path, params: Params) -> dict[str, Any]:
    import torch

    source = Path.cwd()
    sys.path[:0] = [str(source / "hy3dpaint"), str(source)]
    import textureGenPipeline  # pyright: ignore[reportMissingImports]
    import trimesh
    from utils import multiview_utils  # pyright: ignore[reportMissingImports]

    torch.set_grad_enabled(False)
    image = prepare_image((work / "image").read_bytes(), params.remove_background)
    mesh_path = work / "mesh.obj"
    simplify(work / "input.glb", mesh_path, params.max_faces)
    swap_models(textureGenPipeline, multiview_utils)
    config = textureGenPipeline.Hunyuan3DPaintConfig(params.views, params.view_size)
    # Upstream bakes at this size and halves it when it saves (save_mesh
    # downsample=True): its default 4096 gives a 2048 file.
    config.texture_size = params.texture_size * 2
    config.dino_ckpt_path = str(dino_directory())
    config.realesrgan_ckpt_path = str(realesrgan_path())
    pipeline = textureGenPipeline.Hunyuan3DPaintPipeline(config)
    pipeline(
        mesh_path=str(mesh_path),
        image_path=image,
        output_mesh_path=str(work / "textured.obj"),
        use_remesh=False,
        save_glb=True,
    )
    plain_pbr(work / "textured.glb")
    floor_roughness(work / "textured.glb", params.min_roughness, params.max_metallic)
    painted = trimesh.load(work / "textured.glb", force="mesh")
    return {
        "triangles": len(painted.faces),
        "texture_size": painted.visual.material.baseColorTexture.size[0],
        "views": params.views,
        "view_size": params.view_size,
        "materials": "pbr (base colour, metallic, roughness)",
        "min_roughness": params.min_roughness,
        "max_metallic": params.max_metallic,
        "peak_vram_gb": round(torch.cuda.max_memory_allocated() / 2**30, 2),
    }


def main() -> None:
    work, params = Path(sys.argv[1]), Params.model_validate_json(sys.argv[2])
    print(RESULT_PREFIX + json.dumps(paint(work, params)), flush=True)
    exit_now()  # bpy's teardown would crash


if __name__ == "__main__":
    main()
