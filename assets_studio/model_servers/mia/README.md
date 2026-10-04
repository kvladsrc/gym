# Make-It-Animatable model server

Задача `3d-to-rig` (ADR-006) на основе
[Make-It-Animatable v2](https://github.com/jasongzy/Make-It-Animatable)
(CVPR 2025): GLB-сетка персонажа → FBX со скелетом Mixamo и весами кожи.
Анимации Mixamo ложатся на него в Unity (Humanoid) без загрузки в Mixamo.
Только гуманоиды.

```sh
just setup     # исходники v2 (с Hunyuan3D-2.1) и окружение
               # Python 3.11 + PyTorch 2.8/CUDA 12.9
just download  # веса, шаблон скелета, VAE (только сеть)
just doctor    # PyTorch, GPU, bpy, скачанные файлы
just serve     # http://127.0.0.1:9113
just test      # без GPU и весов
```

Скачивается (вне Dropbox):

- исходники, ревизия `bbd8b15`, и веса `jasongzy/Make-It-Animatable`
  (`output/best/v2`): `~/.cache/assets-studio/mia-source`;
- шаблон скелета `bones.fbx` из датасета `jasongzy/Mixamo` — **закрытый**:
  на Hugging Face нужно принять условия тем аккаунтом, чей токен в
  `HF_TOKEN`;
- VAE Hunyuan3D-2.1 (основа модели): `~/.cache/assets-studio/hy3dgen`.

Параметры: `rest_pose` (`t-pose` — результат в T-позе, для Mixamo;
`input` — поза исходной модели), `input_pose` (`auto`, `t-pose`,
`a-pose` — известная поза входа, кости сохраняют её), `fingers` (кости
пальцев). Модель детерминирована: `max_count = 1`.

Шаги — те же, что в демо upstream (`app_v2.py`); экспорт в FBX — его же
`app_blender.py`, но в отдельном процессе: bpy нельзя менять данные не из
главного потока (так делала и v1).

Лицензии: Make-It-Animatable — Apache-2.0; VAE Hunyuan3D-2.1 — Tencent
Hunyuan Community License (не действует в ЕС, Великобритании и Южной
Корее); шаблон скелета — Mixamo.
