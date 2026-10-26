# model_server_sdk

SDK for Asset Studio model servers. Implements
[contract v1](../docs/adr/0001-model-server-contract.md): background model loading,
`/v1/info`, `/v1/generate`, validation, error codes, seeds and generation
serialization. Supports Python 3.11+.

## Writing a model server

A server is a separate project in `model_servers/<name>/`, with its own
`pyproject.toml` and `uv.lock`. Heavy dependencies (PyTorch, CUDA) stay there
and do not enter Studio's workspace environment.

```toml
# model_servers/example/pyproject.toml
[project]
name = "example-model-server"
version = "0.1.0"
requires-python = ">=3.11"
dependencies = ["model-server-sdk", "torch==2.5.1"]

[project.scripts]
example-model-server = "example_model_server.__main__:main"

[tool.uv.sources]
model-server-sdk = { path = "../../model_server_sdk", editable = true }

[build-system]
requires = ["hatchling>=1.25"]
build-backend = "hatchling.build"
```

Implementation consists of task declarations and two methods:

```python
from pydantic import BaseModel, Field
from model_server_sdk import (
    GenerationError,
    InputSpec,
    Job,
    KnownTask,
    ModelInfo,
    ModelServer,
    Output,
    TaskSpec,
    serve,
)


class Params(BaseModel):
    # Every parameter must have a default value.
    resolution: int = Field(default=256, ge=64, le=512, description="Grid")


class ExampleServer(ModelServer):
    model = ModelInfo(
        id="example",
        name="Example",
        revision="<commit>",
        license="MIT",
        source="https://huggingface.co/…",
    )
    tasks = [
        TaskSpec(
            KnownTask.IMAGE_TO_3D,
            Params,
            ("model/gltf-binary",),
            prompt="none",
            inputs=(InputSpec(role="image", mime=["image/png", "image/jpeg"]),),
            max_count=3,
        ),
    ]

    def load(self) -> None: ...  # load weights once, in the background

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        image = job.inputs["image"].data
        results = []
        for seed in job.seeds:  # exactly len(job.seeds) outputs, in order
            results.append(Output("model/gltf-binary", run_model(image, seed, job.params)))
        return results


def main() -> None:
    serve(lambda _: ExampleServer(), default_port=9102, description="Example")
```

Rules:

- `generate` returns **exactly** `len(job.seeds)` outputs; output `i` corresponds
  to `job.seeds[i]`. The SDK adds `seed` to `meta`.
- **Each result depends only on its own seed.** Use one generator per seed,
  not one for the whole batch:

  ```python
  generators = [torch.Generator("cuda").manual_seed(seed) for seed in job.seeds]
  images = pipe(prompt, num_images_per_prompt=len(generators), generator=generators).images
  ```

  Wrong: `generator=torch.Generator().manual_seed(job.seeds[0])` for the entire
  batch; candidate `i` would not be reproducible in a separate request.

- Parameters are flat `str`, `int`, `float`, `bool` or `Literal[...]` fields,
  each with a default. No `Optional`, lists, nested models, `Enum` classes or
  aliases. The SDK validates this at startup.
- Image inputs must accept `image/png`, audio inputs `audio/wav`, and 3D inputs
  `model/gltf-binary`. Additional formats are allowed.
- Failure on a valid request → `GenerationError` (`retryable=True` if retrying
  might help, such as OOM). Formally valid input unsuitable for the model →
  `InvalidInput`. Any other exception becomes `internal`.
- `load` and all `generate` calls run sequentially in one dedicated thread,
  preserving thread-local settings from `load`. Loading has no time limit;
  `/v1/info` reports `loading` meanwhile.
- Values in `meta` must serialize to JSON: use `float(x)`, not `numpy.float32`.
- The server listens on `127.0.0.1`; set its port with `--port` or `MODEL_SERVER_PORT`.

### Primary parameters, text and video (ADR-003, SDK 1.3.0)

- Mark frequently needed parameters (strength, duration, size) as primary;
  Studio shows them directly rather than under “Advanced”:

  ```python
  from model_server_sdk import PRIMARY

  strength: float = Field(default=0.5, ge=0, le=1, json_schema_extra=PRIMARY)
  ```

- New known tasks: `text-to-text`, `image-to-video`, `text-to-3d`.
- Return text as `text/plain` (UTF-8), video as `video/mp4` (H.264, `yuv420p`,
  `+faststart`). The server encodes it; Studio plays it without transcoding.
  `media.SAMPLE_MP4` is a sample clip.

### Bilingual labels, transparency and songs (ADR-005, SDK 1.5.0)

- Parameter `description` is English (read by agents and the English UI).
  Russian labels and the primary marker belong in `ui()`:

  ```python
  from model_server_sdk import ui

  steps: int = Field(
      default=40,
      description="Steps",
      json_schema_extra=ui(ru="Шаги"),
  )
  size: str = Field(
      default="1K",
      description="Size",
      json_schema_extra=ui(ru="Размер", primary=True),
  )
  ```

  Inputs use `InputSpec(..., description="First frame", labels={"ru": "Первый кадр"})`.

- `images.open_image(data, keep_alpha=True)` / `prepare_input(...,
  keep_alpha=True)` retain transparent images as RGBA (for models that read
  alpha themselves, such as Qwen-Image).
- New known task `text-to-song`: the prompt is lyrics, and style is a parameter.

### Character skeletons (ADR-006, SDK 1.6.0)

- File type `model/x-fbx` (binary FBX, `media.FBX_MAGIC`) and known task
  `3d-to-rig`: character GLB → FBX with skeleton and skin weights.

### Video and large models (SDK 1.4.0)

- `model_server_sdk.video.encode_mp4(frames, width=, height=, fps=)` converts
  RGB24 frames to canonical MP4 (H.264 High, yuv420p, `+faststart`), byte-reproducibly:
  identical frames give identical bytes. Requires `ffmpeg` (system installation
  or `video` extra, `imageio-ffmpeg`).
- `model_server_sdk.offload` provides disk block storage for diffusers group
  offload (FLUX, Wan): version-fingerprinted directories, locking, `complete`
  markers and disk-space checks. No torch dependency.

### Images: `model_server_sdk.images`

Since 1.2.0, dependency `model-server-sdk[images]` (Pillow). Shared image helpers:

- `RESOLUTIONS`: SDXL/FLUX sizes (~1 MP) named `"1024x1024"`, etc.
- `prepare_input(data)`: EXIF rotation, transparency composited on white,
  aspect ratio limited to 1:4, ~1 MP, dimensions divisible by 64. Unreadable
  data raises `InvalidInput`.
- `check_denoising(steps, strength)`: rejects image-to-image where SDXL would
  run zero steps (FLUX uses different rounding; see its server).
- `encode_png(image)`: PNG for `Output`.

## Checking a server

Start the server, then check contract compliance:

```sh
just assets_studio check-server http://127.0.0.1:9102
```

The check waits for loading, calls every task with sample inputs and default
parameters, and verifies responses (output count, seeds, MIME, file signatures,
resolved parameters) and error paths. It also checks seed rules: repeated
identical requests must be byte-reproducible, different seeds must differ, and
batch outputs must match single requests.

- If identical seeds are not reproducible, `--allow-nondeterministic` turns
  the error into a warning. This usually indicates a server bug: the generator
  did not receive the seed.
- For inherently deterministic models (for example Piper),
  `--allow-seed-independent` turns the seed error into a warning.
- GPUs do not always produce byte-identical batch and single outputs, so
  differences are warnings by default; `--strict-determinism` makes them errors.
- If defaults are too slow (720p × 5-second video takes about an hour),
  `--param seconds=1.0 --param size=480p` overrides all generations (values
  are JSON or strings). Response checks expect these values instead of defaults.

Passing without errors is required for server acceptance; explain every warning
in the model report.

## Fake server

`model_servers/fake` declares every known task and returns deterministic PNG,
GLB and WAV without a GPU. Parameters `delay_s` and `fail` simulate slow models
and errors. Flags `--load-delay`, `--load-error`, `--model-id` and `--task`
simulate loading, load failure, model replacement and task subsets.

```sh
just assets_studio fake-server --port 9100
```
