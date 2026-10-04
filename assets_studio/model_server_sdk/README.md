# model_server_sdk

SDK для серверов моделей студии ассетов. Реализует
[контракт v1](../docs/adr/0001-model-server-contract.md): фоновую загрузку
модели, `/v1/info`, `/v1/generate`, валидацию, коды ошибок, seed и
блокировку одновременных генераций. Поддерживает Python 3.11+.

## Как написать сервер модели

Сервер — отдельный проект в `model_servers/<имя>/` со своим
`pyproject.toml` и `uv.lock`. Тяжёлые зависимости (PyTorch, CUDA) живут
только там и не попадают в рабочее окружение студии.

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

Реализация — описание задач и два метода:

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
    # У каждого параметра обязательно есть значение по умолчанию.
    resolution: int = Field(default=256, ge=64, le=512, description="Сетка")


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

    def load(self) -> None: ...  # загрузить веса; один раз, в фоне

    def generate(self, job: Job) -> list[Output]:
        assert isinstance(job.params, Params)
        image = job.inputs["image"].data
        results = []
        for seed in job.seeds:  # ровно len(job.seeds) результатов, по порядку
            results.append(Output("model/gltf-binary", run_model(image, seed, job.params)))
        return results


def main() -> None:
    serve(lambda _: ExampleServer(), default_port=9102, description="Example")
```

Правила:

- `generate` возвращает **ровно** `len(job.seeds)` результатов, результат
  `i` — для `job.seeds[i]`. SDK сам добавит `seed` в `meta`.
- **Результат зависит только от своего seed.** Один генератор на seed,
  а не один на весь батч:

  ```python
  generators = [torch.Generator("cuda").manual_seed(seed) for seed in job.seeds]
  images = pipe(prompt, num_images_per_prompt=len(generators), generator=generators).images
  ```

  Неправильно: `generator=torch.Generator().manual_seed(job.seeds[0])` для
  всего батча — тогда кандидат `i` не воспроизводится отдельным запросом.
- Параметры — плоские поля `str`, `int`, `float`, `bool` или `Literal[...]`,
  у каждого есть значение по умолчанию; без `Optional`, списков, вложенных
  моделей, `Enum`-классов и псевдонимов. SDK проверяет это при старте.
- Вход с картинкой обязан принимать `image/png`, с аудио — `audio/wav`,
  с 3D — `model/gltf-binary`. Остальные форматы можно принимать
  дополнительно.
- Модель не справилась с корректным запросом → `GenerationError`
  (`retryable=True`, если повтор может помочь, например при нехватке
  памяти). Вход формально корректен, но не подходит модели →
  `InvalidInput`. Любое другое исключение станет ошибкой `internal`.
- `load` и все вызовы `generate` выполняются по очереди в одном
  выделенном потоке: потоковые настройки из `load` сохраняются. `load`
  может занимать сколько угодно: `/v1/info` в это время отвечает `loading`.
- Значения в `meta` должны сериализоваться в JSON: `float(x)`, а не
  `numpy.float32`.
- Сервер слушает `127.0.0.1`. Порт задаётся `--port` или
  `MODEL_SERVER_PORT`.

### Основные параметры, текст и видео (ADR-003, SDK 1.3.0)

- Параметр, который нужен почти всегда (сила изменения, длительность,
  размер), помечается основным — студия показывает его сразу, а не в
  «Дополнительно»:

  ```python
  from model_server_sdk import PRIMARY

  strength: float = Field(default=0.5, ge=0, le=1, json_schema_extra=PRIMARY)
  ```

- Новые известные задачи: `text-to-text`, `image-to-video`, `text-to-3d`.
- Текст отдаётся как `text/plain` (UTF-8), видео — `video/mp4` (H.264,
  `yuv420p`, `+faststart`): сервер кодирует его сам, студия проигрывает без
  перекодирования. `media.SAMPLE_MP4` — образец такого ролика.

### Подписи на двух языках, прозрачность, песни (ADR-005, SDK 1.5.0)

- `description` параметра — по-английски (его читают агенты и английский
  интерфейс), подпись для русского интерфейса — в `ui()`, там же пометка
  основного параметра:

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

  У входа — `InputSpec(..., description="First frame", labels={"ru": "Первый кадр"})`.
- `images.open_image(data, keep_alpha=True)` / `prepare_input(...,
  keep_alpha=True)` — картинка с прозрачностью остаётся RGBA (для моделей,
  которые сами читают альфу, как Qwen-Image).
- Новая известная задача `text-to-song`: описание — текст песни, стиль —
  параметр.

### Скелет персонажа (ADR-006, SDK 1.6.0)

- Вид файла `model/x-fbx` (двоичный FBX, `media.FBX_MAGIC`) и известная
  задача `3d-to-rig`: GLB-сетка персонажа → FBX со скелетом и весами.

### Видео и большие модели (SDK 1.4.0)

- `model_server_sdk.video.encode_mp4(frames, width=, height=, fps=)` —
  кадры RGB24 в канонический MP4 (H.264 High, yuv420p, `+faststart`),
  побайтно воспроизводимо: те же кадры — те же байты. Нужен `ffmpeg`
  (системный или из extra `video`, `imageio-ffmpeg`).
- `model_server_sdk.offload` — хранилище блоков на диске для group offload
  diffusers (FLUX, Wan): каталог по отпечатку версий, блокировка, отметка
  `complete`, проверка места. Без зависимости от torch.

### Картинки: `model_server_sdk.images`

С версии 1.2.0, зависимость `model-server-sdk[images]` (Pillow). Общее для
серверов картинок:

- `RESOLUTIONS` — размеры SDXL/FLUX (~1 Мпикс) по имени `"1024x1024"` и т. п.;
- `prepare_input(data)` — входная картинка: поворот по EXIF, прозрачность на
  белом фоне, соотношение сторон не круче 1:4, ~1 Мпикс, стороны кратны 64;
  нечитаемые данные — `InvalidInput`;
- `check_denoising(steps, strength)` — отклоняет image-to-image, где SDXL не
  сделает ни одного шага (у FLUX своё округление, см. его сервер);
- `encode_png(image)` — PNG для `Output`.

## Проверка сервера

Запустить сервер и проверить его на соответствие контракту:

```sh
just assets_studio check-server http://127.0.0.1:9102
```

Проверка дожидается загрузки модели, вызывает каждую задачу с тестовыми
входами и параметрами по умолчанию, проверяет ответы (число результатов,
seed, MIME, сигнатуры файлов, фактические параметры) и пути ошибок.
Отдельно проверяются правила seed: одинаковый запрос повторяется
побитово, разные seed дают разные результаты, а результат из батча
совпадает с одиночным запросом.

- Модель не воспроизводит результат при том же seed —
  `--allow-nondeterministic` превращает ошибку в предупреждение. Обычно
  это ошибка в коде сервера: генератор не получил seed.
- Модель детерминирована по природе (например, Piper) —
  `--allow-seed-independent` превращает ошибку seed в предупреждение.
- GPU не всегда даёт побитово одинаковый результат в батче и поодиночке,
  поэтому расхождение по умолчанию — предупреждение; `--strict-determinism`
  делает его ошибкой.
- Модель с параметрами по умолчанию генерирует слишком долго (видео 720p
  × 5 с — около часа) — `--param seconds=1.0 --param size=480p` подставляет
  свои значения во все генерации (значение — JSON или строка); проверка
  ответа ждёт их вместо умолчаний.

Прохождение без ошибок — условие приёмки сервера; каждое предупреждение
нужно объяснить в отчёте о модели.

## Fake-сервер

`model_servers/fake` объявляет все известные задачи и возвращает
детерминированные PNG, GLB и WAV без GPU. Параметры `delay_s` и `fail`
имитируют медленную модель и ошибки; флаги `--load-delay`, `--load-error`,
`--model-id` и `--task` — загрузку, её сбой, замену модели и подмножество
задач.

```sh
just assets_studio fake-server --port 9100
```
