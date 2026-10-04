# Hunyuan3D-Paint model server

Задача `3d-paint`: модель (GLB) и картинка, из которой она сделана → та же
модель с UV-текстурой. На основе
[Hunyuan3D-Paint v2.0 turbo](https://github.com/Tencent-Hunyuan/Hunyuan3D-2):
убирает свет с картинки, рисует шесть видов объекта по нормалям и
положениям сетки и запекает их в текстуру, так что невидимые стороны тоже
окрашены. Пара к Hunyuan3D (тот же кадр, что у его форм).

```sh
just setup     # исходники, окружение Python 3.11 + PyTorch 2.8/CUDA 12.9,
               # сборка CUDA-растеризатора в nix develop .#assets-studio-cuda
just download  # веса paint и delight (~14 ГБ, только сеть)
just doctor
just serve     # http://127.0.0.1:9114
just test      # без GPU и весов
```

- PyTorch для CUDA 12.9, а не 12.4, как у остальных серверов: CUDA 12.4
  удалена из nixpkgs, а растеризатор собирается nvcc из nixpkgs (12.9),
  версии должны совпадать.
- Обе диффузионные модели (~8 ГБ весов fp16) выгружаются в ОЗУ по модулям
  (`enable_model_cpu_offload`): иначе не помещаются в 8 ГБ.
- Картинка без прозрачности проходит rembg; прозрачная берётся как есть.

Лицензия: Tencent Hunyuan Community License (не действует в ЕС,
Великобритании и Южной Корее).
