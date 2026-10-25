# UniRig model server

Задача `3d-to-rig`: модель (GLB) → та же модель со скелетом и весами кожи
(FBX). [UniRig](https://github.com/VAST-AI-Research/UniRig) (VAST и Цинхуа,
SIGGRAPH 2025): скелет предсказывает авторегрессионный трансформер
(OPT-350m, 15 лучей), веса — вторая модель. Скелет любой формы, не Mixamo;
преемник — сервер `skintokens`.

```sh
just setup     # исходники, окружение Python 3.11 + PyTorch 2.8/CUDA 12.9,
               # flash-attn, spconv, torch-scatter/cluster, bpy 4.2 готовыми колёсами
just download  # чекпоинты (~5,8 ГБ) и конфиг OPT-350m (только сеть)
just doctor
just serve     # http://127.0.0.1:9117
just test      # без GPU и весов
```

- Задание — цепочка скриптов upstream, каждый шаг отдельным процессом в
  каталоге исходников: извлечь сетку → скелет → извлечь снова → кожа →
  перенести на исходную модель (текстура сохраняется) → FBX
  (`asset_processing.blender`). Скрипты `launch/` не вызываются: они делают
  `pip install`.
- upstream на каждом запуске спрашивает Hub о чекпоинтах без ревизии;
  шаги идут с `HF_HUB_OFFLINE=1`, и он берёт закреплённые копии из
  `experiments/`.
- Слияние upstream пишет FBX без текстур; поэтому слияние — в GLB, а FBX
  собирает `asset_processing.blender`.

Лицензия: MIT (обучен на Articulation-XL 2.0).
