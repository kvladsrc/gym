# TripoSR model server

Image-to-3D по контракту v1 на основе
[TripoSR](https://github.com/VAST-AI-Research/TripoSR) (MIT). Одна
картинка → GLB (Y-up, цвета вершин, линейный `COLOR_0`).

```sh
just setup    # исходники TripoSR и окружение Python 3.11 + PyTorch 2.5.1/CUDA 12.4
just doctor   # PyTorch, GPU, ревизия исходников, извлечение поверхности
just serve    # http://127.0.0.1:9102; веса загружаются в фоне (~1 мин)
just test     # постобработка сетки, без GPU и весов
```

Рецепты сами входят в `nix develop .#assets-studio-gpu` корневого flake:
колёсам PyTorch нужны C++ runtime и драйвер NVIDIA хоста.

Тяжёлые файлы лежат вне Dropbox:

- окружение Python (~5,6 ГБ, жёсткие ссылки на кеш uv):
  `~/.cache/assets-studio/venvs/triposr`;
- исходники TripoSR, ревизия `107cefd`: `~/.cache/assets-studio/triposr-source`;
- веса `stabilityai/TripoSR`, ревизия `5b52193` (1,6 ГБ):
  `~/.cache/huggingface/hub`;
- модель удаления фона `u2net` (168 МБ): `~/.cache/assets-studio/u2net`.

Параметры: `mc_resolution` (сетка marching cubes, 256), `remove_background`
(rembg, включено), `foreground_ratio` (0,85). TripoSR детерминирована:
seed не влияет на результат, поэтому `max_count = 1`, а проверка
контракта запускается с `--allow-seed-independent`.

Отличия от upstream: `torchmcubes` (C++/CUDA-расширение) подменяется
извлечением поверхности scikit-image на CPU без изменения исходников
TripoSR; предобработка картинки повторяет upstream, но пустой кадр после
удаления фона даёт ошибку `invalid_request` вместо падения.
