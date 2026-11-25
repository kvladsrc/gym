"""FLUX 3 Image Klein server: text-to-image and editing (contract v1).

A 5.6B image transformer, distilled to 16 steps, reading a Qwen3-VL 4B text
encoder, with a prompt upsampler (a Qwen3.5-9B fine-tune) that writes the full
scene caption the model is trained on. On an 8 GB card the transformer runs as
GGUF Q8 (practically lossless); the transformer and the encoder are streamed
to the GPU block by block, their blocks kept on disk rather than in RAM, as in
the Qwen-Image server; the upsampler runs in llama.cpp for the caption only,
before the image model takes the GPU.

The weights are under the FLUX Kommunity License: non-commercial use, plus
commercial use of outputs for qualifying users (under US$5M annual revenue).
"""

import contextlib
import gc
import logging
import math
from typing import Any, Literal

from model_server_sdk.images import MAX_ASPECT, encode_png, open_image
from PIL import Image, ImageOps
from pydantic import BaseModel, ConfigDict, Field

from flux3_model_server.upsampler import Upsampler
from model_server_sdk import (
    GenerationError,
    InputSpec,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
    offload,
    ui,
)

logger = logging.getLogger("flux3_model_server")

BASE_REPO = "black-forest-labs/flux-3-image-klein"
# Git revisions, not secrets.
BASE_REVISION = "3b767ff9b5261d71645e2ec49d87d298d955238d"  # pragma: allowlist secret
GGUF_REPO = "unsloth/flux-3-image-klein-GGUF"
GGUF_REVISION = "12ef13ce920d53f45fe805093910c899668b9e76"  # pragma: allowlist secret
GGUF_FILE = "flux-3-image-klein-Q8_0.gguf"
UPSAMPLER_REPO = "unsloth/flux-3-image-klein-pup-GGUF"
UPSAMPLER_REVISION = "3dc3ad42e7ad7491e4b18ee350caba0afb9bdd9d"  # pragma: allowlist secret
UPSAMPLER_FILE = "flux-3-image-klein-pup-Q8_0.gguf"
UPSAMPLER_PROJECTOR = "mmproj-F16.gguf"
UPSAMPLER_PROMPTS = "system_prompts.json"
# Everything but the bf16 transformer weights, which the GGUF file replaces.
BASE_FILES = [
    "modular_model_index.json",
    "text_encoder/*",
    "vae/*",
    "transformer/config.json",
]
# The offload store holds the transformer (~6.1 GB as GGUF) and the encoder
# (~8.9 GB in bf16, its output head left out).
OFFLOAD_BYTES = 16 * 10**9
# How the models are split into offload groups and how their tensors are
# prepared; part of the store's identity, since the store's files are named
# after the groups and hold the tensors as prepared.
OFFLOAD_LAYOUT = "groups1-plain-bf16"
STEPS = 16  # the distilled model's flow map; it takes no guidance

# About one megapixel or four, every side a multiple of 16, with the aspect
# ratio the upsampler composes for.
SIZES = {
    "1024x1024": "1:1",
    "1152x864": "4:3",
    "864x1152": "3:4",
    "1248x832": "3:2",
    "832x1248": "2:3",
    "1360x768": "16:9",
    "768x1360": "9:16",
    "2048x2048": "1:1",
    "2304x1728": "4:3",
    "1728x2304": "3:4",
    "2496x1664": "3:2",
    "1664x2496": "2:3",
    "2720x1536": "16:9",
    "1536x2720": "9:16",
}
Size = Literal[
    "1024x1024",
    "1152x864",
    "864x1152",
    "1248x832",
    "832x1248",
    "1360x768",
    "768x1360",
    "2048x2048",
    "2304x1728",
    "1728x2304",
    "2496x1664",
    "1664x2496",
    "2720x1536",
    "1536x2720",
]
# Edits keep the source's aspect ratio at about this many pixels.
EDIT_PIXELS = {"1K": 1024 * 1024, "2K": 2048 * 2048}
GRID = 16  # sides of generated images are multiples of this
# Images up to about one megapixel go through the float32 VAE whole, as in
# BFL's code. A 2K image would need 4 GB at once in the decoder and does not
# fit beside the server's resident parts: the VAE then encodes and decodes it
# in its own 1024 px tiles with a quarter overlap.
WHOLE_VAE_PIXELS = 1_100_000
MAX_SIDE = 4096


class TextToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    resolution: Size = Field(
        default="1024x1024", description="Size", json_schema_extra=ui(ru="Размер", primary=True)
    )
    upsample: bool = Field(
        default=True,
        description="Expand the prompt into a full scene caption (off only for a ready caption)",
        json_schema_extra=ui(ru="Расписать подсказку (выключать только для готового описания)"),
    )


class ImageToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    size: Literal["1K", "2K"] = Field(
        default="1K",
        description="Size (in the source image's proportions)",
        json_schema_extra=ui(ru="Размер (пропорции исходной картинки)", primary=True),
    )
    upsample: bool = Field(
        default=True,
        description="Expand the instruction into a full caption (off only for a ready caption)",
        json_schema_extra=ui(ru="Расписать задание (выключать только для готового описания)"),
    )


IMAGE_MIMES = ["image/png", "image/jpeg", "image/webp"]
TASKS = (
    TaskSpec(KnownTask.TEXT_TO_IMAGE, TextToImageParams, ("image/png",), prompt="required"),
    TaskSpec(
        KnownTask.IMAGE_TO_IMAGE,
        ImageToImageParams,
        ("image/png",),
        # An edit is an instruction: without one there is nothing to do.
        prompt="required",
        inputs=(
            InputSpec(role="image", mime=IMAGE_MIMES, description="The image to edit"),
            InputSpec(
                role="reference",
                mime=IMAGE_MIMES,
                required=False,
                description="A second image to take a subject, garment or style from",
                labels={"ru": "Вторая картинка: откуда взять персонажа, одежду или стиль"},
            ),
        ),
    ),
)


def decode_unquantized_gguf(module: Any, dtype: Any) -> list[str]:
    """Turn the GGUF file's unquantized tensors (BF16, F16, F32) into plain
    tensors of ``dtype``; returns their names.

    diffusers keeps them as raw bytes (uint8) like the quantized ones. FLUX 3
    keeps its only other unquantized weights, the attention norms, in float32,
    so the model found no floating-point weight to report as its dtype and
    said uint8, and the pipeline cast its latents to uint8. Plain bf16
    weights take as much memory as the raw bytes, and GGUF linear layers use
    them unchanged."""
    import torch
    from diffusers.quantizers.gguf.utils import (
        UNQUANTIZED_TYPES,
        GGUFParameter,
        dequantize_gguf_tensor,
    )

    decoded: list[str] = []
    for module_name, child in module.named_modules():
        for name, parameter in list(child.named_parameters(recurse=False)):
            if isinstance(parameter, GGUFParameter) and parameter.quant_type in UNQUANTIZED_TYPES:
                plain = dequantize_gguf_tensor(parameter).as_subclass(torch.Tensor).to(dtype)
                setattr(child, name, torch.nn.Parameter(plain, requires_grad=False))
                decoded.append(f"{module_name}.{name}" if module_name else name)
    return decoded


def edit_source(data: bytes) -> Image.Image:
    """An edit's image, RGB over white (FLUX 3 has no alpha channel),
    cropped only beyond 1:4 / 4:1."""
    image = open_image(data, keep_alpha=True)
    if image.mode == "RGBA":
        white = Image.new("RGBA", image.size, (255, 255, 255, 255))
        image = Image.alpha_composite(white, image)
    image = image.convert("RGB")
    aspect = image.width / image.height
    clamped = min(max(aspect, 1 / MAX_ASPECT), MAX_ASPECT)
    if clamped == aspect:
        return image
    if aspect > clamped:  # too wide
        size = (max(1, round(image.height * clamped)), image.height)
    else:  # too tall
        size = (image.width, max(1, round(image.width / clamped)))
    return ImageOps.fit(image, size, Image.Resampling.LANCZOS)


def fitted_size(width: int, height: int, pixels: int) -> tuple[int, int]:
    """The source's proportions at about ``pixels``, sides on the 16-pixel
    grid and at most 4096."""
    scale = math.sqrt(pixels / (width * height))
    scale = min(scale, MAX_SIDE / max(width, height))
    return (
        max(GRID, round(width * scale / GRID) * GRID),
        max(GRID, round(height * scale / GRID) * GRID),
    )


def resized_source(image: Image.Image, pixels: int) -> Image.Image:
    """A single reference at the edit's size in its exact proportions: the
    pipeline pads it to the grid and crops the result back to it."""
    scale = min(math.sqrt(pixels / (image.width * image.height)), MAX_SIDE / max(image.size))
    size = (max(GRID, round(image.width * scale)), max(GRID, round(image.height * scale)))
    return image if size == image.size else image.resize(size, Image.Resampling.LANCZOS)


class Flux3Server(ModelServer):
    model = ModelInfo(
        id="flux-3-image-klein-q8",
        name="FLUX 3 Image Klein (Q8)",
        revision=f"{BASE_REVISION[:8]}+gguf.{GGUF_REVISION[:8]}",
        license="FLUX Kommunity License (non-commercial; outputs for qualifying users)",
        source=f"https://huggingface.co/{BASE_REPO}",
        details={
            "transformer": "GGUF Q8_0 (unsloth/flux-3-image-klein-GGUF), practically lossless",
            "text encoder": "Qwen3-VL-4B-Instruct, bf16",
            "prompt upsampler": "flux-3-image-klein-pup (Qwen3.5-9B), GGUF Q8_0 in llama.cpp",
            "offloading": "transformer and encoder streamed from disk block by block",
            "steps": f"{STEPS} (distilled flow map, no guidance)",
            "GPU": "8 GB card",
        },
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._pipeline: Any = None
        self._encoder: Any = None
        self._tokenizer: Any = None
        self._upsampler: Upsampler | None = None
        # Holds the offload store's lock for the server's lifetime.
        self._resources = contextlib.ExitStack()

    def load(self) -> None:
        import diffusers
        import torch
        import transformers
        from diffusers import Flux3Transformer2DModel, GGUFQuantizationConfig, ModularPipeline
        from diffusers.hooks import apply_group_offloading
        from huggingface_hub import hf_hub_download, snapshot_download
        from llm_model_server import runtime
        from transformers import AutoProcessor, Qwen3VLForConditionalGeneration

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("FLUX 3 needs a CUDA GPU")
        gpu, cpu = torch.device("cuda"), torch.device("cpu")
        runtime.install()
        base = snapshot_download(BASE_REPO, revision=BASE_REVISION, allow_patterns=BASE_FILES)
        weights = hf_hub_download(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION)
        self._upsampler = Upsampler(
            *(
                hf_hub_download(UPSAMPLER_REPO, name, revision=UPSAMPLER_REVISION)
                for name in (UPSAMPLER_FILE, UPSAMPLER_PROJECTOR, UPSAMPLER_PROMPTS)
            )
        )

        # Offloaded blocks live on disk, not in RAM. The store is tied to
        # these exact weights and library versions (see offload.py).
        store_base = offload.base_directory("flux3")
        self._resources.enter_context(offload.lock(store_base))
        store, complete = offload.prepare(
            store_base,
            offload.fingerprint(
                BASE_REVISION[:8],
                GGUF_REVISION[:8],
                GGUF_FILE,
                f"diffusers{diffusers.__version__}",
                f"transformers{transformers.__version__}",
                f"torch{torch.__version__}",
                OFFLOAD_LAYOUT,
            ),
            needed_bytes=OFFLOAD_BYTES,
        )
        if not complete:
            logger.info("writing the offload store to %s (~15 GB, once)", store)

        transformer = Flux3Transformer2DModel.from_single_file(
            weights,
            quantization_config=GGUFQuantizationConfig(compute_dtype=torch.bfloat16),
            torch_dtype=torch.bfloat16,
            config=base,
            subfolder="transformer",
        )
        decoded = decode_unquantized_gguf(transformer, torch.bfloat16)
        logger.info("decoded %d unquantized GGUF tensors to bf16", len(decoded))
        if transformer.dtype != torch.bfloat16:
            raise RuntimeError(f"the transformer reports {transformer.dtype}, not bfloat16")
        # Group offload moves whole blocks with .to(); accelerate's model and
        # sequential offload cannot move GGUF parameters. Offloaded before the
        # encoder is loaded, so the two never sit in RAM together.
        transformer.enable_group_offload(
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            offload_to_disk_path=str(store / "transformer"),
        )

        encoder = Qwen3VLForConditionalGeneration.from_pretrained(
            base, subfolder="text_encoder", dtype=torch.bfloat16
        )
        # FLUX 3 reads intermediate hidden states, never the token logits: the
        # output head would only be streamed in for nothing.
        encoder.lm_head = torch.nn.Identity()
        # The token embedding gets a group of its own: the model embeds the
        # tokens before it calls the language model's blocks. The vision tower
        # is never called (FLUX 3 encodes text only) but must leave RAM too.
        for name, part, own_groups in (
            ("language", encoder.model.language_model, ["embed_tokens"]),
            ("vision", encoder.model.visual, None),
        ):
            apply_group_offloading(
                part,
                onload_device=gpu,
                offload_device=cpu,
                offload_type="block_level",
                num_blocks_per_group=1,
                offload_to_disk_path=str(store / name),
                block_modules=own_groups,
            )
        if not complete:
            offload.mark_complete(store)
        self._encoder = encoder
        self._tokenizer = AutoProcessor.from_pretrained(base, subfolder="text_encoder")

        # The image pipeline without the upsampler (llama.cpp writes the
        # caption) and without the text encoder step (the caption is encoded
        # once for all variants).
        blocks = ModularPipeline.from_pretrained(base).blocks
        blocks.sub_blocks.pop("prompt_upsampler")
        blocks.sub_blocks.pop("text_encoder")
        pipeline = blocks.init_pipeline(base)
        pipeline.load_components(names=["vae"], dtype=torch.float32)
        pipeline.update_components(transformer=transformer)
        pipeline.vae.to(gpu)
        self._pipeline = pipeline

    def generate(self, job: Job) -> list[Output]:
        import torch

        # Inputs first: a bad image must not cost an upsampler run.
        roles = [role for role in ("image", "reference") if role in job.inputs]
        references = [edit_source(job.inputs[role].data) for role in roles]
        out_of_memory: str | None = None
        try:
            if isinstance(job.params, TextToImageParams):
                return self._text(job, job.params)
            assert isinstance(job.params, ImageToImageParams)
            return self._edit(job, job.params, references)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) and retry", retryable=True
        )

    def _caption(
        self, prompt: str, upsample: bool, references: list[Image.Image], canvas: str | None
    ) -> tuple[str, dict[str, Any]]:
        if not upsample:
            return prompt.strip(), {}
        assert self._upsampler is not None
        return self._upsampler.caption(prompt, references, canvas)

    def _encode(self, caption: str) -> Any:
        import torch
        from diffusers.modular_pipelines.flux3.encoders import Flux3TextEncoderStep

        return Flux3TextEncoderStep._get_qwen3_vl_prompt_embeds(
            text_encoder=self._encoder,
            tokenizer=self._tokenizer,
            prompt=caption,
            device=torch.device("cuda"),
        )

    def _text(self, job: Job, params: TextToImageParams) -> list[Output]:
        width, height = (int(side) for side in params.resolution.split("x"))
        canvas = SIZES[params.resolution]
        self._pipeline.vae.use_tiling = width * height > WHOLE_VAE_PIXELS
        caption, meta = self._caption(
            job.prompt or "", params.upsample, [], None if canvas == "1:1" else canvas
        )
        embeds = self._encode(caption)
        outputs = []
        for seed in job.seeds:
            image = self._pipeline(
                prompt_embeds=embeds,
                width=width,
                height=height,
                generator=_generator(seed),
                output="images",
            )[0]
            outputs.append(_png(image, caption, params.upsample, meta))
        return outputs

    def _edit(
        self, job: Job, params: ImageToImageParams, references: list[Image.Image]
    ) -> list[Output]:
        caption, meta = self._caption(job.prompt or "", params.upsample, references, None)
        embeds = self._encode(caption)
        pixels = EDIT_PIXELS[params.size]
        self._pipeline.vae.use_tiling = pixels > WHOLE_VAE_PIXELS
        size: dict[str, int] = {}
        if len(references) == 1:
            # One reference keeps its exact proportions: padded, then cropped back.
            images: Any = resized_source(references[0], pixels)
        else:
            # Several are each fitted under one megapixel by the pipeline; the
            # result takes the first one's proportions at the edit's size.
            images = references
            width, height = fitted_size(*references[0].size, pixels)
            size = {"width": width, "height": height}
        outputs = []
        for seed in job.seeds:
            image = self._pipeline(
                prompt_embeds=embeds,
                image=images,
                generator=_generator(seed),
                output="images",
                **size,
            )[0]
            outputs.append(_png(image, caption, params.upsample, meta))
        return outputs


def _generator(seed: int) -> Any:
    import torch

    # A CPU generator: the same seed gives the same noise on any device.
    return torch.Generator(device="cpu").manual_seed(seed)


def _png(image: Image.Image, caption: str, upsampled: bool, meta: dict[str, Any]) -> Output:
    image = image.convert("RGB")
    return Output(
        "image/png",
        encode_png(image),
        {
            "width": image.width,
            "height": image.height,
            "steps": STEPS,
            "upsampled": upsampled,
            # What the image model received: the upsampler's caption or the prompt.
            "caption": caption,
            **meta,
        },
    )
