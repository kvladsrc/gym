# Hunyuan3D-2.1 Shape model server

Задача `image-to-3d`: картинка → форма (GLB без цвета).
[Hunyuan3D-2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1) Shape — самая
большая открытая модель формы Hunyuan3D (июнь 2025): DiT на 3,3 млрд
параметров против 0,6 млрд у 2mini, тот же рецепт. Цвет — через `3d-paint`
(свой PBR-покрасчик 2.1 требует ~21 ГБ видеопамяти).

```sh
just setup     # исходники, окружение Python 3.11 + PyTorch 2.5.1/CUDA 12.4
just download  # чекпоинт DiT с VAE и кодировщиком картинки (~7,4 ГБ)
just doctor
just serve     # http://127.0.0.1:9119
just test      # без GPU и весов
```

- Официально ~10 ГБ видеопамяти. Здесь части конвейера по очереди
  занимают GPU (кодировщик картинки → DiT → VAE), остальное ждёт в ОЗУ;
  веса в fp16, без квантования. Пик ~7,1 ГБ, загрузка 34 с.
- Свой `enable_model_cpu_offload` у upstream — копия из diffusers и здесь
  не работает (у конвейера нет `components`), а хуки accelerate пропустили
  бы VAE (его зовут через `latents2mesh`, не `forward`): сервер сам
  оборачивает `encode_cond`, `prepare_latents` и `_export`.
- Один seed даёт ту же форму (те же числа вершин и треугольников, те же
  границы), но не тот же GLB до байта: недетерминированные ядра GPU.
  Проверка контракта — с `--allow-nondeterministic`.
- Веса лежат в `HY3DGEN_MODELS` (`~/.cache/assets-studio/hy3dgen`) — там же
  VAE 2.1, который использует `mia`.

Лицензия: Tencent Hunyuan 3D 2.1 Community License (не действует в ЕС,
Великобритании и Южной Корее), как у 2.0.
