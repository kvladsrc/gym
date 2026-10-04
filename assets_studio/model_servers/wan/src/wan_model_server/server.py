"""Wan2.2 TI2V-5B server: video from a first frame (contract v1, ADR-003).

The 5B video transformer is quantized to GGUF Q8 (5.4 GB, practically
lossless) and kept in ordinary RAM; its blocks are pinned as they are sent
and stream to the GPU on a CUDA stream, the next one while the current one
computes. The card is left to
the activations: a 5 s 720p clip is ~27 000 tokens, and with the whole
transformer resident it ran out of memory. (Blocks streamed from an on-disk
store without overlap made the GPU wait: 13 min for 1 s at 480p.) The
UMT5-XXL text encoder (11.4 GB), used once per request, streams from an
on-disk store as in the FLUX server; the fp32 VAE comes to the GPU layer by
layer, to encode and decode in tiles. 24 fps, ~720p, up to 5 s.
"""

import contextlib
import gc
import logging
import math
from typing import Any, Literal

from model_server_sdk.images import MAX_ASPECT, open_image
from model_server_sdk.video import encode_mp4
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

logger = logging.getLogger("wan_model_server")

REPO = "Wan-AI/Wan2.2-TI2V-5B-Diffusers"
# Git revisions, not secrets.
REVISION = "b8fff7315c768468a5333511427288870b2e9635"  # pragma: allowlist secret
GGUF_REPO = "unsloth/Wan2.2-TI2V-5B-GGUF"
GGUF_REVISION = "b5c2a3816e7056e57e200f6be726fb36ce523b49"  # pragma: allowlist secret
GGUF_FILE = "Wan2.2-TI2V-5B-Q8_0.gguf"
# Everything but the transformer weights, which the GGUF file replaces.
FILES = [
    "model_index.json",
    "scheduler/*",
    "tokenizer/*",
    "text_encoder/*",
    "transformer/config.json",
    "vae/*",
]
FPS = 24  # TI2V-5B is trained at 24 frames per second
# Pixel areas of the two sizes; the sides follow the first frame's aspect.
AREAS = {"720p": 1280 * 704, "480p": 832 * 480}
# Sides must be multiples of the VAE's 16× downscale times the 2×2 patch.
SIDE_MULTIPLE = 32
MAX_SEQUENCE_LENGTH = 512
# The offload store holds UMT5 (~11.4 GB in bf16).
OFFLOAD_BYTES = 13 * 10**9
# Wan's own default negative prompt (`sample_neg_prompt`), in the original
# Chinese the model was tuned with. In English: bright tones, overexposed,
# static, blurred details, subtitles, style, works, paintings, images, still,
# overall gray, worst quality, low quality, JPEG artifacts, ugly, incomplete,
# extra fingers, poorly drawn hands and faces, deformed, disfigured, misshapen
# limbs, fused fingers, still picture, messy background, three legs, many
# people in the background, walking backwards.
DEFAULT_NEGATIVE = (
    "色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，"
    "最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，"
    "画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，"
    "杂乱的背景，三条腿，背景人很多，倒着走"
)

Size = Literal["720p", "480p"]


class Params(BaseModel):
    model_config = ConfigDict(extra="forbid")

    seconds: float = Field(
        default=5.0,
        ge=1.0,
        le=5.0,
        description="Duration, s",
        json_schema_extra=ui(ru="Длительность, с", primary=True),
    )
    size: Size = Field(
        default="720p",
        description="Size (480p: faster, a draft)",
        json_schema_extra=ui(ru="Размер (480p — быстрее, черновик)", primary=True),
    )
    steps: int = Field(
        default=50, ge=10, le=100, description="Steps", json_schema_extra=ui(ru="Шаги")
    )
    guidance: float = Field(
        default=5.0,
        ge=1,
        le=10,
        description="Prompt adherence",
        json_schema_extra=ui(ru="Следование описанию"),
    )
    negative_prompt: str = Field(
        default=DEFAULT_NEGATIVE,
        description="What to avoid",
        json_schema_extra=ui(ru="Чего избегать"),
    )


TASK = TaskSpec(
    KnownTask.IMAGE_TO_VIDEO,
    Params,
    ("video/mp4",),
    prompt="optional",
    inputs=(
        InputSpec(
            role="image",
            mime=["image/png", "image/jpeg", "image/webp"],
            description="First frame",
            labels={"ru": "Первый кадр"},
        ),
    ),
)


def frame_count(seconds: float) -> int:
    """The 4k + 1 frames (the VAE packs 4 after the first) nearest to
    ``seconds`` at 24 fps."""
    return max(1, round((seconds * FPS - 1) / 4)) * 4 + 1


def video_size(width: int, height: int, size: Size) -> tuple[int, int]:
    """Sides with the image's aspect (clamped to 1:4…4:1) and about the size's
    area, as Wan's own example computes them; the frame is then cropped to
    them, not stretched."""
    area = AREAS[size]
    aspect = min(max(height / width, 1 / MAX_ASPECT), MAX_ASPECT)
    new_height = round(math.sqrt(area * aspect)) // SIDE_MULTIPLE * SIDE_MULTIPLE
    new_width = round(math.sqrt(area / aspect)) // SIDE_MULTIPLE * SIDE_MULTIPLE
    return max(new_width, SIDE_MULTIPLE), max(new_height, SIDE_MULTIPLE)


class WanServer(ModelServer):
    model = ModelInfo(
        id="wan2-2-ti2v-5b",
        name="Wan2.2 TI2V 5B",
        revision=f"{REVISION[:8]}+gguf.{GGUF_REVISION[:8]}",
        license="Apache-2.0",
        source=f"https://huggingface.co/{REPO}",
    )
    tasks = (TASK,)

    def __init__(self) -> None:
        self._pipeline: Any = None
        # Holds the offload store's lock for the server's lifetime.
        self._resources = contextlib.ExitStack()

    def load(self) -> None:
        import diffusers
        import torch
        import transformers
        from diffusers import (
            AutoencoderKLWan,
            GGUFQuantizationConfig,
            WanImageToVideoPipeline,
            WanTransformer3DModel,
        )
        from diffusers.hooks import apply_group_offloading
        from huggingface_hub import hf_hub_download, snapshot_download
        from transformers import UMT5EncoderModel

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("Wan needs a CUDA GPU (on the CPU a clip takes days)")
        gpu, cpu = torch.device("cuda"), torch.device("cpu")
        path = snapshot_download(REPO, revision=REVISION, allow_patterns=FILES)
        weights = hf_hub_download(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION)

        store_base = offload.base_directory("wan")
        self._resources.enter_context(offload.lock(store_base))
        store, complete = offload.prepare(
            store_base,
            offload.fingerprint(
                REVISION[:8],
                "umt5",
                f"diffusers{diffusers.__version__}",
                f"transformers{transformers.__version__}",
                f"torch{torch.__version__}",
            ),
            needed_bytes=OFFLOAD_BYTES,
        )
        if not complete:
            logger.info("writing the offload store to %s (~11 GB, once)", store)

        transformer = WanTransformer3DModel.from_single_file(
            weights,
            quantization_config=GGUFQuantizationConfig(compute_dtype=torch.bfloat16),
            torch_dtype=torch.bfloat16,
            config=path,
            subfolder="transformer",
        )
        transformer.enable_group_offload(
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            use_stream=True,  # prefetch the next block while one computes
            # Each block is pinned as it is sent. Without this diffusers pins a
            # second copy of the whole model up front (free RAM fell to 1 GB);
            # pinning the model in place once did not help either (it still
            # re-pins: free RAM fell to 4 GB). The price: 413 s instead of 254 s
            # for a 1 s 480p clip.
            low_cpu_mem_usage=True,
        )
        text_encoder = UMT5EncoderModel.from_pretrained(
            path, subfolder="text_encoder", torch_dtype=torch.bfloat16
        )
        # As T5 in FLUX: the blocks are a ModuleList inside the encoder stack.
        apply_group_offloading(
            text_encoder.encoder,
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            offload_to_disk_path=str(store / "umt5"),
        )
        if not complete:
            offload.mark_complete(store)
        # Wan's VAE is meant to run in fp32 (2.8 GB): it would not fit beside
        # the transformer, so its layers come to the GPU as they run (it runs
        # twice per clip: encoding the first frame, decoding the video).
        vae = AutoencoderKLWan.from_pretrained(path, subfolder="vae", torch_dtype=torch.float32)
        vae.enable_tiling()
        # Twice per clip: no stream (and no pinned copy) needed.
        vae.enable_group_offload(onload_device=gpu, offload_device=cpu, offload_type="leaf_level")
        self._pipeline = WanImageToVideoPipeline.from_pretrained(
            path,
            transformer=transformer,
            text_encoder=text_encoder,
            vae=vae,
            image_encoder=None,
            image_processor=None,
            torch_dtype=torch.bfloat16,
        )
        self._pipeline.set_progress_bar_config(disable=True)

    def generate(self, job: Job) -> list[Output]:
        import torch

        assert isinstance(job.params, Params)
        params = job.params
        # Inputs first: a bad image must not cost a pass over ~11 GB of encoder.
        first = open_image(job.inputs["image"].data)
        width, height = video_size(first.width, first.height, params.size)
        # Scaled to cover and centre-cropped, as Wan's own code does: the
        # sides are rounded, and a stretched first frame would warp the clip.
        first = ImageOps.fit(first, (width, height), Image.Resampling.LANCZOS)
        frames = frame_count(params.seconds)
        out_of_memory: str | None = None
        try:
            embeds, negative = self._encode(job.prompt or "", params)
            return [self._clip(first, embeds, negative, params, frames, seed) for seed in job.seeds]
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        finally:
            torch.cuda.empty_cache()  # other model servers share the GPU
            # Blocks are pinned as they are sent; PyTorch caches that page-locked
            # memory (~9 GB after a clip) until told otherwise. Private in
            # torch 2.5, hence the guard.
            release_pinned = getattr(torch._C, "_host_emptyCache", None)
            if release_pinned is not None:
                release_pinned()
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) or use 480p",
            retryable=True,
        )

    def _encode(self, prompt: str, params: Params) -> tuple[Any, Any]:
        """Prompt embeddings, once per request for all variants."""
        import torch

        return self._pipeline.encode_prompt(
            prompt=prompt,
            negative_prompt=params.negative_prompt,
            do_classifier_free_guidance=params.guidance > 1,
            num_videos_per_prompt=1,
            max_sequence_length=MAX_SEQUENCE_LENGTH,
            device=torch.device("cuda"),
        )

    def _clip(
        self, first: Image.Image, embeds: Any, negative: Any, params: Params, frames: int, seed: int
    ) -> Output:
        import numpy as np
        import torch

        video = self._pipeline(
            image=first,
            prompt_embeds=embeds,
            negative_prompt_embeds=negative,
            height=first.height,
            width=first.width,
            num_frames=frames,
            num_inference_steps=params.steps,
            guidance_scale=params.guidance,
            generator=torch.Generator(device="cpu").manual_seed(seed),
            output_type="np",
        ).frames[0]
        pixels = (np.clip(video, 0, 1) * 255).round().astype(np.uint8)
        if len(pixels) != frames:
            raise GenerationError(f"the model returned {len(pixels)} frames, not {frames}")
        data = encode_mp4(
            (frame.tobytes() for frame in pixels), width=first.width, height=first.height, fps=FPS
        )
        return Output(
            "video/mp4",
            data,
            {
                "width": first.width,
                "height": first.height,
                "frames": frames,
                "fps": FPS,
                "seconds": round(frames / FPS, 2),
            },
        )
