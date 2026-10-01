"""SDXL base 1.0 server: text-to-image and image-to-image (contract v1).

Both tasks share one set of weights. Candidates are generated one at a time,
each with its own generator seeded with its seed: a batch of four 1024²
images would not fit into 8 GB, and a single request with a candidate's
seed reproduces it exactly.
"""

import gc
import logging
from typing import Any, Literal

from model_server_sdk.images import RESOLUTIONS, check_denoising, encode_png, prepare_input
from PIL import Image
from pydantic import BaseModel, ConfigDict, Field

from model_server_sdk import (
    PRIMARY,
    GenerationError,
    InputSpec,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    is_out_of_memory,
)

logger = logging.getLogger("sdxl_model_server")

BASE_REPO = "stabilityai/stable-diffusion-xl-base-1.0"
# Git revisions, not secrets.
BASE_REVISION = "462165984030d82259a11f4367a4eed129e94a7b"  # pragma: allowlist secret
# The original SDXL VAE overflows in fp16 (black images); this one does not.
VAE_REPO = "madebyollin/sdxl-vae-fp16-fix"
VAE_REVISION = "207b116dae70ace3637169f1ddd2434b91b3a8cd"  # pragma: allowlist secret
# Only the fp16 weights the pipeline loads (~7 GB instead of ~20 GB).
BASE_FILES = [
    "model_index.json",
    "scheduler/*",
    "tokenizer/*",
    "tokenizer_2/*",
    "text_encoder/config.json",
    "text_encoder/model.fp16.safetensors",
    "text_encoder_2/config.json",
    "text_encoder_2/model.fp16.safetensors",
    "unet/config.json",
    "unet/diffusion_pytorch_model.fp16.safetensors",
]
VAE_FILES = ["config.json", "diffusion_pytorch_model.safetensors"]

Resolution = Literal[
    "1024x1024", "1152x896", "896x1152", "1216x832", "832x1216", "1344x768", "768x1344"
]


class Common(BaseModel):
    model_config = ConfigDict(extra="forbid")

    steps: int = Field(default=30, ge=1, le=100, description="Шаги")
    # At 1 and below classifier-free guidance is off: the negative prompt is ignored.
    guidance: float = Field(default=6.0, ge=1, le=20, description="Следование описанию")
    negative_prompt: str = Field(default="", description="Чего избегать")


class TextToImageParams(Common):
    resolution: Resolution = Field(
        default="1024x1024", description="Размер", json_schema_extra=PRIMARY
    )


class ImageToImageParams(Common):
    strength: float = Field(
        default=0.5,
        ge=0.05,
        le=1,
        description="Сила изменения (1 — исходная картинка почти не учитывается)",
        json_schema_extra=PRIMARY,
    )


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


class SdxlServer(ModelServer):
    model = ModelInfo(
        id="sdxl-base-1",
        name="SDXL base 1.0",
        # Both pins: a different VAE is a different model for reproducibility.
        revision=f"{BASE_REVISION[:8]}+vae.{VAE_REVISION[:8]}",
        license="CreativeML Open RAIL++-M",
        source=f"https://huggingface.co/{BASE_REPO}",
    )
    tasks = TASKS

    def __init__(self) -> None:
        self._text_to_image: Any = None
        self._image_to_image: Any = None

    def load(self) -> None:
        import torch
        from diffusers import (
            AutoencoderKL,
            StableDiffusionXLImg2ImgPipeline,
            StableDiffusionXLPipeline,
        )
        from huggingface_hub import snapshot_download

        torch.set_grad_enabled(False)  # the SDK runs load() and generate() on one thread
        cuda = torch.cuda.is_available()
        if not cuda:
            logger.warning("CUDA is not available: SDXL on the CPU takes minutes per image")
        # fp16 kernels are GPU-only; on the CPU the fp16 weights are upcast.
        dtype = torch.float16 if cuda else torch.float32
        base = snapshot_download(BASE_REPO, revision=BASE_REVISION, allow_patterns=BASE_FILES)
        vae_dir = snapshot_download(VAE_REPO, revision=VAE_REVISION, allow_patterns=VAE_FILES)
        vae = AutoencoderKL.from_pretrained(vae_dir, torch_dtype=dtype)
        pipeline = StableDiffusionXLPipeline.from_pretrained(
            base, vae=vae, torch_dtype=dtype, variant="fp16", use_safetensors=True
        )
        if cuda:
            # Moves whole models (text encoders → UNet → VAE) to the GPU only for
            # their stage: the pipeline (~7 GB) plus activations would not fit.
            pipeline.enable_model_cpu_offload()
        self._text_to_image = pipeline
        # The same components (and the offload hooks on them), no extra memory.
        # Not from_pipe(): it rebuilds the offload chain so that the text
        # encoders stay on the GPU while the UNet loads — out of memory on 8 GB.
        # This pipeline has no hook list of its own, so the text-to-image one
        # frees the models after each image-to-image call (see _free).
        self._image_to_image = StableDiffusionXLImg2ImgPipeline(**pipeline.components)

    def generate(self, job: Job) -> list[Output]:
        import torch

        out_of_memory: str | None = None
        try:
            if isinstance(job.params, TextToImageParams):
                return self._text(job, job.params)
            assert isinstance(job.params, ImageToImageParams)
            try:
                return self._image(job, job.params)
            finally:
                # This pipeline has no hook list of its own (see load).
                self._free()
        except (torch.cuda.OutOfMemoryError, RuntimeError) as error:
            if not is_out_of_memory(error):
                self._free()
                raise
            # Keep only the text: the exception's traceback holds every frame of
            # the failed call with its activations, and they must be freed below.
            out_of_memory = str(error)
            logger.warning("out of GPU memory: %s", out_of_memory)
        # Here the exception and its frames are gone (``error`` is unbound at the
        # end of the except block): the cache can really be returned.
        self._free()
        gc.collect()
        torch.cuda.empty_cache()
        raise GenerationError(
            "out of GPU memory; free GPU memory (other model servers) or use a smaller size",
            retryable=True,
        )

    def _free(self) -> None:
        """Move every model back to the CPU (after image-to-image and after a
        failure mid-call, otherwise the UNet can stay on the GPU under the next
        call's encoders). Never masks the error being handled."""
        if self._text_to_image is None:
            return
        try:
            self._text_to_image.maybe_free_model_hooks()
        except Exception:
            logger.exception("could not move the models back to the CPU")

    def _text(self, job: Job, params: TextToImageParams) -> list[Output]:
        width, height = RESOLUTIONS[params.resolution]
        outputs: list[Output] = []
        for seed in job.seeds:
            image = self._text_to_image(
                prompt=job.prompt,
                negative_prompt=params.negative_prompt or None,
                width=width,
                height=height,
                num_inference_steps=params.steps,
                guidance_scale=params.guidance,
                generator=_generator(seed),
            ).images[0]
            outputs.append(_png(image, steps=params.steps, denoising=params.steps))
        return outputs

    def _image(self, job: Job, params: ImageToImageParams) -> list[Output]:
        denoising = check_denoising(params.steps, params.strength)
        source = prepare_input(job.inputs["image"].data)
        outputs: list[Output] = []
        for seed in job.seeds:
            image = self._image_to_image(
                prompt=job.prompt or "",
                negative_prompt=params.negative_prompt or None,
                image=source,
                strength=params.strength,
                num_inference_steps=params.steps,
                guidance_scale=params.guidance,
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
