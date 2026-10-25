# Kimodo model server

Задача `text-to-motion`: описание движения (по-английски) → анимация
гуманоида (FBX: скелет в T-позе и движение, 30 кадров в секунду).
[Kimodo](https://github.com/nv-tlabs/kimodo) (NVIDIA, 2026) — диффузионная
модель движения, обучена на ~700 часах захвата движений; предложения через
точку — движения подряд, с переходами.

```sh
just setup     # исходники, окружение Python 3.11 + PyTorch 2.8/CUDA 12.9,
               # сборка Kimodo с MotionCorrection (C++) в nix develop .#assets-studio-cuda
just download  # Kimodo (~1,1 ГБ), адаптеры LLM2Vec, Llama-3-8B (~16 ГБ);
               # нужен HF_TOKEN
just doctor
just serve     # http://127.0.0.1:9118
just test      # без GPU и весов
```

- Скелет Kimodo — SOMA (77 суставов); кости переименованы в имена Mixamo
  (`mixamorig:Hips`…, `to_fbx.py`): Unity сам строит по ним аватар
  Humanoid, и клип играет на любом гуманоиде, в том числе на ригах `mia`.
- Текстовый кодировщик — LLM2Vec на Llama-3-8B-Instruct, на CPU (~16 ГБ
  ОЗУ, один проход на промпт); на GPU только Kimodo (< 3 ГБ). Llama 3 —
  закрытый репозиторий: доступ на Hugging Face одобряет Meta.
- MotionCorrection (против проскальзывания ступней) собирается с
  `-include stdlib.h`: её `Scalar.h` зовёт `abs()` без заголовка.

Лицензии: Kimodo — NVIDIA Open Model License (коммерческое использование
разрешено, результаты не принадлежат NVIDIA); адаптеры LLM2Vec — MIT;
Llama 3 — Meta Llama 3 Community License.

Для готового FBX персонажа передайте результат Kimodo и риг MIA в
[animation_tools](../animation_tools/README.md), задачу `retarget-motion`.
Она учитывает позы покоя и пропорции, переносит вращения и возвращает
персонажа с клипом. Цепочку можно сохранить в [studio flow](../../flow/README.md).
Внешний вызов Blender-скрипта для этого не нужен.

## Sparse pose / hand / foot constraints

`text-to-motion` optionally accepts a `constraints` text/plain JSON input using
Kimodo's official list format (`root2d`, `fullbody`, hand/foot, `end-effector`).
With constraints, use a single motion description. Frame indices are 0-based
inside the generated clip; values are finite SOMA 30/77-joint axis-angle poses
and root positions in Y-up metres, not raw Mixamo/MIA bone Euler rotations.
The input is validated before inference; model revision includes `.constraints.1`.
Metadata records constraint groups, frame indices and a canonical JSON hash.
Existing text-only generation is unchanged; no SDK modifications.

Keyframe constraints require a correct target-to-SOMA pose conversion. Correct
reference stances first, then supply selected good phases as intermediate keys.
Kimodo has no independent sword object constraint. Target rig contact, wrist
rotation and weapon orientation need separate validation after retargeting.
