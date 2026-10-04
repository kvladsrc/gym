"""Qwen-Image 2.1 server: text-to-image and instruction editing (contract v1).

A 7B image transformer reading a Qwen3-VL 8B encoder, with a VAE that has an
alpha channel: images with real transparency, legible text, native 2K, and
editing in words ("make the lantern blue"). On an 8 GB card the transformer
runs as GGUF Q8 (practically lossless) and both big models are streamed to
the GPU block by block, their blocks kept on disk rather than in RAM, as in
the FLUX server. Quality over speed was the explicit choice.

The weights are under the Qwen Research License: research and other
non-commercial use.
"""

import contextlib
import gc
import logging
from collections.abc import Iterator
from typing import Any, Literal

from model_server_sdk.images import MAX_ASPECT, encode_png, open_image
from PIL import Image, ImageOps
from pydantic import BaseModel, ConfigDict, Field

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

logger = logging.getLogger("qwen_image_model_server")

BASE_REPO = "Qwen/Qwen-Image-2.1"
# Git revisions, not secrets.
BASE_REVISION = "d26bb61231c349cf6b7896fa83353113880e1ba3"  # pragma: allowlist secret
GGUF_REPO = "unsloth/Qwen-Image-2.1-GGUF"
GGUF_REVISION = "2c31ccd392b367a6637841a143813320a02dff55"  # pragma: allowlist secret
GGUF_FILE = "qwen-image-2.1-Q8_0.gguf"
# Everything but the bf16 transformer weights, which the GGUF file replaces.
BASE_FILES = [
    "model_index.json",
    "processor/*",
    "scheduler/*",
    "text_encoder/*",
    "vae/*",
    "transformer/config.json",
]
# The offload store holds the transformer (~7.6 GB as GGUF) and the encoder
# without its unused output head (~16.3 GB in bf16).
OFFLOAD_BYTES = 26 * 10**9
# How the models are split into offload groups; part of the store's identity,
# since the store's files are named after the groups.
OFFLOAD_LAYOUT = "groups3"

# The prompt format the model card gives for images with a transparent background.
TRANSPARENT_PROMPT = (
    "This is an RGBA image with transparency. {prompt}. "
    "The image has alpha channel and the background is transparent."
)

# About one megapixel (1K) or four (2K, the model's native size), in the
# aspect ratios of the model card, every side a multiple of 32.
SIZES = {
    "1024x1024": (1024, 1024),
    "1152x896": (1152, 896),
    "896x1152": (896, 1152),
    "1216x832": (1216, 832),
    "832x1216": (832, 1216),
    "1376x768": (1376, 768),
    "768x1376": (768, 1376),
    "2048x2048": (2048, 2048),
    "2304x1792": (2304, 1792),
    "1792x2304": (1792, 2304),
    "2432x1664": (2432, 1664),
    "1664x2432": (1664, 2432),
    "2752x1536": (2752, 1536),
    "1536x2752": (1536, 2752),
}
Size = Literal[
    "1024x1024",
    "1152x896",
    "896x1152",
    "1216x832",
    "832x1216",
    "1376x768",
    "768x1376",
    "2048x2048",
    "2304x1792",
    "1792x2304",
    "2432x1664",
    "1664x2432",
    "2752x1536",
    "1536x2752",
]
# Edits keep the input's aspect ratio at about this many pixels.
EDIT_SIZES = {"1K": 1024, "2K": 2048}
# The pipeline caches keys and values of the text and the condition image for
# all steps: ~0.5 MB per token. At 1K that is ~2.6 GB; at 2K the condition
# alone is ~20k tokens (~10 GB), more than the card: 2K edits recompute it.
KV_CACHE_SIZES = {"1K"}


class TextToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    resolution: Size = Field(
        default="2048x2048", description="Size", json_schema_extra=ui(ru="Размер", primary=True)
    )
    transparent: bool = Field(
        default=False,
        description="Transparent background",
        json_schema_extra=ui(ru="Прозрачный фон"),
    )
    steps: int = Field(
        default=40, ge=10, le=60, description="Steps", json_schema_extra=ui(ru="Шаги")
    )


class ImageToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    size: Literal["1K", "2K"] = Field(
        default="2K",
        description="Size (in the source image's proportions)",
        json_schema_extra=ui(ru="Размер (пропорции исходной картинки)", primary=True),
    )
    transparent: bool = Field(
        default=False,
        description="Transparent background",
        json_schema_extra=ui(ru="Прозрачный фон"),
    )
    steps: int = Field(
        default=40, ge=10, le=60, description="Steps", json_schema_extra=ui(ru="Шаги")
    )


TASKS = (
    TaskSpec(KnownTask.TEXT_TO_IMAGE, TextToImageParams, ("image/png",)),
    TaskSpec(
        KnownTask.IMAGE_TO_IMAGE,
        ImageToImageParams,
        ("image/png",),
        # An edit is an instruction: without one there is nothing to do.
        prompt="required",
        inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    ),
)


def full_prompt(prompt: str, transparent: bool) -> str:
    return TRANSPARENT_PROMPT.format(prompt=prompt.strip().rstrip(".")) if transparent else prompt


def decode_stray_gguf(module: Any, dtype: Any) -> list[str]:
    """Decode GGUF parameters outside GGUF linear layers into plain tensors.

    diffusers dequantizes GGUF weights inside its GGUFLinear layers only; any
    other module reads the raw bytes. This checkpoint stores the text
    projection's RMSNorm weight in BF16, so the norm would see 8192 "values"
    for its 4096 channels. Returns the names of the decoded parameters."""
    import torch
    from diffusers.quantizers.gguf.utils import GGUFLinear, GGUFParameter, dequantize_gguf_tensor

    decoded: list[str] = []
    for module_name, child in module.named_modules():
        if isinstance(child, GGUFLinear):
            continue
        for name, parameter in list(child.named_parameters(recurse=False)):
            if isinstance(parameter, GGUFParameter):
                plain = dequantize_gguf_tensor(parameter).as_subclass(torch.Tensor).to(dtype)
                setattr(child, name, torch.nn.Parameter(plain, requires_grad=False))
                decoded.append(f"{module_name}.{name}" if module_name else name)
    return decoded


def separate_shared_storage(module: Any) -> int:
    """Give every parameter that shares its storage with another one a copy
    of its own (same type and values); returns how many were copied."""
    from collections import Counter

    parameters = list(module.parameters())
    owners = Counter(parameter.data.untyped_storage().data_ptr() for parameter in parameters)
    copied = 0
    for parameter in parameters:
        if owners[parameter.data.untyped_storage().data_ptr()] > 1:
            parameter.data = parameter.data.clone()
            copied += 1
    return copied


def edit_source(data: bytes) -> Image.Image:
    """The image to edit, at its own size (the pipeline scales it to the edit
    size), alpha kept, cropped only beyond 1:4 / 4:1."""
    image = open_image(data, keep_alpha=True)
    aspect = image.width / image.height
    clamped = min(max(aspect, 1 / MAX_ASPECT), MAX_ASPECT)
    if clamped == aspect:
        return image
    if aspect > clamped:  # too wide
        size = (max(1, round(image.height * clamped)), image.height)
    else:  # too tall
        size = (image.width, max(1, round(image.width / clamped)))
    return ImageOps.fit(image, size, Image.Resampling.LANCZOS)


# Alpha below this is transparency; above it, the VAE's noise on opaque images
# (measured: a few percent of pixels at 203-254).
TRANSPARENT_BELOW = 128
# In a transparent result, alpha at or above this is the opaque body's noise
# (measured on a sprite: 5.7 % of pixels at 240-254, real soft edges and glass
# spread below, <1 %): it is made fully opaque.
OPAQUE_FROM = 240
# Below this, the background's noise (measured: 40-70 % of a transparent
# result's pixels at 1-31, a faint grid among them): it is cleared to 0.
CLEAR_BELOW = 32


def has_transparency(image: Image.Image) -> bool:
    """Whether some pixel is really see-through, not just alpha noise."""
    if image.mode != "RGBA":
        return False
    return sum(image.getchannel("A").histogram()[:TRANSPARENT_BELOW]) > 0


def finished(image: Image.Image, *, keep_alpha: bool) -> Image.Image:
    """The decoded image as it is stored. The VAE always decodes an alpha
    channel, which is noisy on opaque images: alpha is kept only where
    transparency was asked for or the edited image had it, and only if the
    result has some."""
    if not (keep_alpha and has_transparency(image)):
        return image.convert("RGB")
    image = image.copy()
    image.putalpha(
        image.getchannel("A").point(
            lambda a: 255 if a >= OPAQUE_FROM else 0 if a < CLEAR_BELOW else a
        )
    )
    return image


class QwenImageServer(ModelServer):
    model = ModelInfo(
        id="qwen-image-2_1-q8",
        name="Qwen-Image 2.1 (Q8)",
        revision=f"{BASE_REVISION[:8]}+gguf.{GGUF_REVISION[:8]}",
        license="Qwen Research License (non-commercial)",
        source=f"https://huggingface.co/{BASE_REPO}",
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._pipeline: Any = None
        # Holds the offload store's lock for the server's lifetime.
        self._resources = contextlib.ExitStack()

    def load(self) -> None:
        import diffusers
        import torch
        import transformers
        from diffusers import (
            GGUFQuantizationConfig,
            QwenImage21Pipeline,
            QwenImage21Transformer2DModel,
        )
        from diffusers.hooks import apply_group_offloading
        from huggingface_hub import hf_hub_download, snapshot_download

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Qwen-Image needs a CUDA GPU (on the CPU an image takes hours)")
        gpu, cpu = torch.device("cuda"), torch.device("cpu")
        base = snapshot_download(BASE_REPO, revision=BASE_REVISION, allow_patterns=BASE_FILES)
        weights = hf_hub_download(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION)

        # Offloaded blocks live on disk, not in RAM: the two models (~25 GB)
        # would not fit beside the rest of the desktop. The store is tied to
        # these exact weights and library versions (see offload.py).
        store_base = offload.base_directory("qwen_image")
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
            logger.info("writing the offload store to %s (~25 GB, once)", store)

        transformer = QwenImage21Transformer2DModel.from_single_file(
            weights,
            quantization_config=GGUFQuantizationConfig(compute_dtype=torch.bfloat16),
            torch_dtype=torch.bfloat16,
            config=base,
            subfolder="transformer",
        )
        # The checkpoint converter splits fused projections with chunk(): the
        # halves share one storage, which safetensors cannot write to the store.
        separate_shared_storage(transformer)
        decode_stray_gguf(transformer, torch.bfloat16)
        # Offloaded before the encoder is loaded, so the two never sit in RAM
        # together. Group offload moves whole blocks with .to(); accelerate's
        # model and sequential offload cannot move GGUF parameters.
        transformer.enable_group_offload(
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            offload_to_disk_path=str(store / "transformer"),
        )
        pipeline = QwenImage21Pipeline.from_pretrained(
            base, transformer=transformer, torch_dtype=torch.bfloat16
        )
        encoder = pipeline.text_encoder
        # The pipeline reads the encoder's last hidden states, never its token
        # logits: the output head (1.2 GB) would only be streamed in for nothing.
        encoder.lm_head = torch.nn.Identity()
        # The encoder's blocks are ModuleLists inside its language and vision
        # models, not direct children. The token embedding gets a group of its
        # own: the model embeds the tokens before it calls the language model,
        # whose leftover group would load only then.
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
        pipeline.vae.to(gpu)
        # Decoding a whole 1024² image at once needs more than the ~1.5 GB left
        # beside the encoder's and transformer's resident parts; tiles of 768 px
        # overlapping by 128 px are blended, which keeps the seams invisible.
        pipeline.vae.enable_tiling(
            tile_sample_min_height=768,
            tile_sample_min_width=768,
            tile_sample_stride_height=640,
            tile_sample_stride_width=640,
        )
        self._pipeline = pipeline

    def generate(self, job: Job) -> list[Output]:
        import torch

        # Inputs first: a bad image must not cost an encoder pass over ~16 GB of weights.
        source = edit_source(job.inputs["image"].data) if "image" in job.inputs else None
        out_of_memory: str | None = None
        try:
            if isinstance(job.params, TextToImageParams):
                return self._text(job, job.params)
            assert isinstance(job.params, ImageToImageParams)
            assert source is not None
            return self._edit(job, job.params, source)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        # The block that was on the GPU stays there until the next call (a few
        # hundred MB); everything else is released here.
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) and retry", retryable=True
        )

    def _text(self, job: Job, params: TextToImageParams) -> list[Output]:
        import torch

        width, height = SIZES[params.resolution]
        # The prompt is encoded once for all variants.
        embeds, mask, _ = self._pipeline.encode_prompt(
            prompt=full_prompt(job.prompt or "", params.transparent),
            device=torch.device("cuda"),
        )
        outputs: list[Output] = []
        for seed in job.seeds:
            image = self._pipeline(
                prompt_embeds=embeds,
                prompt_embeds_mask=mask,
                width=width,
                height=height,
                num_inference_steps=params.steps,
                generator=_generator(seed),
            ).images[0]
            outputs.append(_png(image, steps=params.steps, keep_alpha=params.transparent))
        return outputs

    def _edit(self, job: Job, params: ImageToImageParams, source: Image.Image) -> list[Output]:
        outputs: list[Output] = []
        # The pipeline encodes the instruction together with the image, and
        # takes no ready embeddings with an image: encode once, reuse for all
        # variants.
        with _encode_once(self._pipeline):
            for seed in job.seeds:
                image = self._pipeline(
                    prompt=full_prompt(job.prompt or "", params.transparent),
                    image=source,
                    output_resolution=EDIT_SIZES[params.size],
                    use_kv_cache=params.size in KV_CACHE_SIZES,
                    num_inference_steps=params.steps,
                    generator=_generator(seed),
                ).images[0]
                outputs.append(
                    _png(
                        image,
                        steps=params.steps,
                        keep_alpha=params.transparent or has_transparency(source),
                    )
                )
        return outputs


@contextlib.contextmanager
def _encode_once(pipeline: Any) -> Iterator[None]:
    """Within the block, every encode_prompt call returns the first call's
    result: the instruction and the image are the same for every variant."""
    encode = pipeline.encode_prompt
    result: list[Any] = []

    def cached(*args: Any, **kwargs: Any) -> Any:
        if not result:
            result.append(encode(*args, **kwargs))
        return result[0]

    pipeline.encode_prompt = cached
    try:
        yield
    finally:
        del pipeline.encode_prompt  # back to the class's method


def _generator(seed: int) -> Any:
    import torch

    # A CPU generator: the same seed gives the same noise on any device.
    return torch.Generator(device="cpu").manual_seed(seed)


def _png(image: Image.Image, *, steps: int, keep_alpha: bool) -> Output:
    image = finished(image, keep_alpha=keep_alpha)
    return Output(
        "image/png",
        encode_png(image),
        {
            "width": image.width,
            "height": image.height,
            "steps": steps,
            "transparent": image.mode == "RGBA",
        },
    )
