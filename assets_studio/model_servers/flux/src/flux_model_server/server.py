"""FLUX.1-schnell server: text-to-image and image-to-image (contract v1).

FLUX follows prompts closely but is large: a 12B transformer and a 4.7B T5
text encoder, ~34 GB in bf16. On an 8 GB card it runs with the transformer
quantized to GGUF Q8 (practically lossless) and both big models streamed to
the GPU block by block (group offload), their blocks kept on disk rather than
in RAM: 55-70 s per 1024² image. Quality over speed was the explicit choice;
Q3/Q4 would be faster or not fit.
"""

import contextlib
import gc
import logging
from typing import Any, Literal

from model_server_sdk.images import RESOLUTIONS, encode_png, prepare_input
from PIL import Image
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

logger = logging.getLogger("flux_model_server")

BASE_REPO = "black-forest-labs/FLUX.1-schnell"
# Git revisions, not secrets.
BASE_REVISION = "741f7c3ce8b383c54771c7003378a50191e9efe9"  # pragma: allowlist secret
GGUF_REPO = "city96/FLUX.1-schnell-gguf"
GGUF_REVISION = "f495746ed9c5efcf4661f53ef05401dceadc17d2"  # pragma: allowlist secret
GGUF_FILE = "flux1-schnell-Q8_0.gguf"
# Everything but the bf16 transformer weights, which the GGUF file replaces.
BASE_FILES = [
    "model_index.json",
    "scheduler/*",
    "tokenizer/*",
    "tokenizer_2/*",
    "text_encoder/*",
    "text_encoder_2/*",
    "vae/*",
    "transformer/config.json",
]
MAX_SEQUENCE_LENGTH = 256  # T5 tokens of the prompt; schnell's maximum
# The offload store holds the transformer (~12.7 GB) and T5 (~8.9 GB).
OFFLOAD_BYTES = 23 * 10**9

Resolution = Literal[
    "1024x1024", "1152x896", "896x1152", "1216x832", "832x1216", "1344x768", "768x1344"
]


class TextToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    resolution: Resolution = Field(
        default="1024x1024", description="Size", json_schema_extra=ui(ru="Размер", primary=True)
    )
    # schnell is distilled for 4 steps; more rarely helps.
    steps: int = Field(default=4, ge=1, le=8, description="Steps", json_schema_extra=ui(ru="Шаги"))


class ImageToImageParams(BaseModel):
    model_config = ConfigDict(extra="forbid")

    strength: float = Field(
        default=0.6,
        ge=0.05,
        le=1,
        description="Change strength (1: the source image is almost ignored)",
        json_schema_extra=ui(
            ru="Сила изменения (1 — исходная картинка почти не учитывается)", primary=True
        ),
    )
    steps: int = Field(default=4, ge=1, le=8, description="Steps", json_schema_extra=ui(ru="Шаги"))


TASKS = (
    TaskSpec(KnownTask.TEXT_TO_IMAGE, TextToImageParams, ("image/png",)),
    TaskSpec(
        KnownTask.IMAGE_TO_IMAGE,
        ImageToImageParams,
        ("image/png",),
        prompt="optional",
        inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg", "image/webp"]),),
    ),
)


def denoising_steps(steps: int, strength: float) -> int:
    """Steps FluxImg2ImgPipeline really runs: it skips ``int(steps - steps *
    strength)`` of the schedule, so at any strength above 0 at least one runs.
    (SDXL rounds differently: see model_server_sdk.images.denoising_steps.)"""
    return steps - int(max(steps - steps * strength, 0))


class FluxServer(ModelServer):
    model = ModelInfo(
        id="flux-schnell-q8",
        name="FLUX.1 schnell (Q8)",
        revision=f"{BASE_REVISION[:8]}+gguf.{GGUF_REVISION[:8]}",
        license="Apache-2.0",
        source=f"https://huggingface.co/{BASE_REPO}",
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._text_to_image: Any = None
        self._image_to_image: Any = None
        # Holds the offload store's lock for the server's lifetime.
        self._resources = contextlib.ExitStack()

    def load(self) -> None:
        import diffusers
        import torch
        import transformers
        from diffusers import (
            FluxImg2ImgPipeline,
            FluxPipeline,
            FluxTransformer2DModel,
            GGUFQuantizationConfig,
        )
        from diffusers.hooks import apply_group_offloading
        from huggingface_hub import hf_hub_download, snapshot_download

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        if not torch.cuda.is_available():
            raise RuntimeError("FLUX needs a CUDA GPU (on the CPU an image takes hours)")
        gpu, cpu = torch.device("cuda"), torch.device("cpu")
        base = snapshot_download(BASE_REPO, revision=BASE_REVISION, allow_patterns=BASE_FILES)
        weights = hf_hub_download(GGUF_REPO, GGUF_FILE, revision=GGUF_REVISION)

        # Offloaded blocks live on disk, not in RAM: both models together
        # (~22 GB) would not fit beside the rest of the desktop, and the kernel
        # would kill the server. The store is tied to these exact weights and
        # library versions (see offload.py).
        store_base = offload.base_directory("flux")
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
            ),
            needed_bytes=OFFLOAD_BYTES,
        )
        if not complete:
            logger.info("writing the offload store to %s (~22 GB, once)", store)

        transformer = FluxTransformer2DModel.from_single_file(
            weights,
            quantization_config=GGUFQuantizationConfig(compute_dtype=torch.bfloat16),
            torch_dtype=torch.bfloat16,
            config=base,
            subfolder="transformer",
        )
        # Offloaded before T5 is loaded, so the two never sit in RAM together.
        # Group offload moves whole blocks with .to(); accelerate's model and
        # sequential offload cannot move GGUF parameters.
        transformer.enable_group_offload(
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            offload_to_disk_path=str(store / "transformer"),
        )
        pipeline = FluxPipeline.from_pretrained(
            base, transformer=transformer, torch_dtype=torch.bfloat16
        )
        # T5's 24 blocks are a ModuleList inside its T5Stack, not a direct
        # child; its token embedding is tied into that stack too.
        apply_group_offloading(
            pipeline.text_encoder_2.encoder,
            onload_device=gpu,
            offload_device=cpu,
            offload_type="block_level",
            num_blocks_per_group=1,
            offload_to_disk_path=str(store / "t5"),
        )
        if not complete:
            offload.mark_complete(store)
        pipeline.text_encoder.to(gpu)
        pipeline.vae.to(gpu)
        self._text_to_image = pipeline
        # The same components; no extra memory.
        self._image_to_image = FluxImg2ImgPipeline(**pipeline.components)

    def generate(self, job: Job) -> list[Output]:
        import torch

        # Inputs first: a bad image must not cost a T5 pass over ~9 GB of weights.
        source = prepare_input(job.inputs["image"].data) if "image" in job.inputs else None
        out_of_memory: str | None = None
        try:
            embeddings = self._encode(job.prompt or "")
            if isinstance(job.params, TextToImageParams):
                return self._text(job, job.params, embeddings)
            assert isinstance(job.params, ImageToImageParams)
            assert source is not None
            return self._image(job, job.params, embeddings, source)
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                raise
            # Keep only the text: the traceback would pin the failed call's tensors.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        # The block that was on the GPU stays there until the next FLUX call
        # (a few hundred MB); everything else is released here.
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) and retry", retryable=True
        )

    def _encode(self, prompt: str) -> dict[str, Any]:
        """Prompt embeddings, computed once per request for all its variants."""
        import torch

        embeds, pooled, _ = self._text_to_image.encode_prompt(
            prompt=prompt,
            prompt_2=None,
            device=torch.device("cuda"),
            max_sequence_length=MAX_SEQUENCE_LENGTH,
        )
        return {"prompt_embeds": embeds, "pooled_prompt_embeds": pooled}

    def _text(
        self, job: Job, params: TextToImageParams, embeddings: dict[str, Any]
    ) -> list[Output]:
        width, height = RESOLUTIONS[params.resolution]
        outputs: list[Output] = []
        for seed in job.seeds:
            image = self._text_to_image(
                **embeddings,
                width=width,
                height=height,
                num_inference_steps=params.steps,
                guidance_scale=0.0,  # schnell is guidance-distilled
                max_sequence_length=MAX_SEQUENCE_LENGTH,
                generator=_generator(seed),
            ).images[0]
            outputs.append(_png(image, steps=params.steps, denoising=params.steps))
        return outputs

    def _image(
        self,
        job: Job,
        params: ImageToImageParams,
        embeddings: dict[str, Any],
        source: Image.Image,
    ) -> list[Output]:
        denoising = denoising_steps(params.steps, params.strength)
        outputs: list[Output] = []
        for seed in job.seeds:
            image = self._image_to_image(
                **embeddings,
                image=source,
                width=source.width,
                height=source.height,
                strength=params.strength,
                num_inference_steps=params.steps,
                guidance_scale=0.0,
                max_sequence_length=MAX_SEQUENCE_LENGTH,
                generator=_generator(seed),
            ).images[0]
            outputs.append(_png(image, steps=params.steps, denoising=denoising))
        return outputs


def _generator(seed: int) -> Any:
    import torch

    # A CPU generator: the same seed gives the same noise on any device.
    return torch.Generator(device="cpu").manual_seed(seed)


def _png(image: Image.Image, *, steps: int, denoising: int) -> Output:
    return Output(
        "image/png",
        encode_png(image),
        {
            "width": image.width,
            "height": image.height,
            "steps": steps,
            "denoising_steps": denoising,
        },
    )
