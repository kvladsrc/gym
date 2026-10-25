# TripoSG model server

> Retired from the active Studio catalog; weights and local environment removed.

Задача `image-to-3d`: картинка → форма (GLB без цвета). На основе
[TripoSG](https://github.com/VAST-AI-Research/TripoSG) от VAST (авторы
Tripo): трансформер rectified flow на 1,5 млрд параметров выбирает
латентное представление формы по картинке, VAE достраивает поверхность на
сетке 512³. Диффузионная модель, как Hunyuan3D: seed даёт разные варианты.
Цвет — через `3d-paint` (Hunyuan3D-Paint).

```sh
just setup     # исходники, окружение Python 3.11 + PyTorch 2.8/CUDA 12.9,
               # сборка diso в nix develop .#assets-studio-cuda
just download  # веса (~8 ГБ fp32, только сеть)
just doctor
just serve     # http://127.0.0.1:9115
just test      # без GPU и весов
```

- Коммерческие Tripo 2.x/3.x закрыты (только API); TripoSG — открытая
  модель Tripo, её README требует 8 ГБ видеопамяти.
- Веса в fp32, сервер держит их в fp16 (~4 ГБ) целиком на GPU.
- Картинку готовят как при обучении: объект без фона, обрезан, по центру
  квадрата с полями 10 % на белом. Фон убирает rembg (IS-Net, как у
  Hunyuan3D), а не BRIA RMBG-1.4 из скриптов TripoSG: у той лицензия
  некоммерческая.
- diso (marching cubes на CUDA) собирается nvcc из nixpkgs (12.9), отсюда
  PyTorch для CUDA 12.9, как у Hunyuan3D-Paint.

Лицензия: MIT (код и веса).
